using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Jobben
{
    [BurstCompatible]
    struct PathJob : IJob
    {
        [ReadOnly]
        private bool log;
        [ReadOnly]
        private readonly MapData data;
        [ReadOnly]
        private readonly Tile start;
        [ReadOnly]
        private readonly Tile goal;
        [ReadOnly]
        private readonly int frontierSize;
        [ReadOnly]
        private NativeArray<Tile> tiles;

        [WriteOnly]
        private NativeList<Tile> result;

        public PathJob(Tile start, Tile goal, NativeArray<Tile> tiles, NativeList<Tile> result, MapData data, bool log = false)
        {
            this.data = data;
            this.start = start;
            this.goal = goal;
            this.tiles = tiles;
            this.result = result;
            frontierSize = data.size.x * data.size.y * data.size.z / 32;
            this.log = log;            
        }

        [BurstCompatible]
        public void Execute()
        {
            Tile begin = tiles[Graph.CalculateIndex(start, data.size)];
            Tile target = tiles[Graph.CalculateIndex(goal, data.size)];
            TileCompare comparer = new TileCompare(target, data);
            Random random = new Random((uint)start.data.x * (uint)start.data.y * (uint)start.data.z + 1);
            NativeArray<int> cameFrom = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeArray<int> costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);                       
            NativeHeap<Tile, TileCompare> frontier = new NativeHeap<Tile, TileCompare>(Allocator.Temp, frontierSize, comparer);          
            for (int i = 0; i < tiles.Length; i++)
            {
                costSoFar[i] = int.MaxValue;
                cameFrom[i] = -1;
            }
            Tile current = begin;
            cameFrom[current.index] = current.index;
            costSoFar[current.index] = 0;
            frontier.Insert(current);  // The frontier sorts nodes by their Manhattan distance shortest to longest with TileCompare.           

            while (frontier.Count > 0)  // Still have nodes to search.
            {
                current = frontier.Pop();
                string msg = "";

                if (frontier.Count >= frontierSize)
                {
                    target = current;
                    if (log) { Debug.Log(this + $" frontier limit {frontierSize} reached. Shortening path."); }
                }

                if (current.Equals(target)) { break; }    // Early exit, goal found.				

                NativeList<Tile> neighbors = GetNeighbors(current); // Filter available neighbors               
                if (log) { msg += ("----Examining: " + current + " with " + neighbors.Length + " neighbors: "); }                

                while (neighbors.Length > 0)  // Loop through all available neighbors
                {
                    int neighborIndex = random.NextInt(0, neighbors.Length);
                    Tile next = neighbors[neighborIndex];
                    int costToNeighbor = Graph.Cost(next - current, data);
                    int newCost = costSoFar[current.index] + costToNeighbor;

                    if (newCost < costSoFar[next.index])
                    {
                        // We haven't been through this neighbor yet, or have found a shorter path
                        costSoFar[next.index] = newCost;
                        cameFrom[next.index] = current.index;
                        frontier.Insert(in next);
                    }

                    neighbors.RemoveAt(neighborIndex);
                    if (log) 
                    {
                        Debug.DrawLine(Graph.TileToWorld(current, data), Graph.TileToWorld(next, data), Color.red, 5f);
                        msg += $"--{next} cost {costSoFar[next.index]}"; 
                    }
                }

                if (log) { Debug.Log(msg + $". Frontier: {frontier.Count}/{frontierSize}"); msg = ""; }
                neighbors.Dispose();
            }

            int items = 0;

            while (!current.Equals(begin))
            {
                if (log) { Debug.Log($"Adding {current} to result array."); }
                result.Add(tiles[current.index]);
                current = tiles[cameFrom[current.index]];
                items++;
            }           

            if (log) { Debug.Log($"Adding {begin} to result array."); }
            result.Add(begin);
            frontier.Dispose();
            costSoFar.Dispose();
            cameFrom.Dispose();
        }
        
        /// <summary>
        /// Returns only valid (movable) neighbor copies from the grid. Checks for node edges and grid limits.
        /// </summary>
        private NativeList<Tile> GetNeighbors(Tile tile, int dropDepth = 2)
        {
            var directions = new NativeList<Tile>(10, Allocator.Temp)
            {   // These should be in the same order as the Edges enum for the bit-shift looping to work
                Tile.n, Tile.e, Tile.s, Tile.w, Tile.ne, Tile.se, Tile.sw, Tile.nw, Tile.up, Tile.down 
            };  

            var neighbors = new NativeList<Tile>(10, Allocator.Temp);

            for (int i = 0; i < directions.Length; i++)
            {
                Edge current = (Edge)(1 << i);
                Tile neighbor = tile + directions[i];

                // if (HasTile(neighbor) && t.HasEdge(current) && !t.occupied) For posterity.
                // Manage with just checking the edge and relying on setting them up accurately.
                if (tile.HasEdge(current) && !tile.occupied)
                {
                    //if (directions[i].Equals(Tile.down))    // If going down, dont allow more than dropDepth steps
                    //{
                    //    for (int j = 1; j < dropDepth; j++)
                    //    {
                    //        int indexOfDrop = Graph.CalculateIndex(tile + directions[i] * j, data.size);                           
                    //        if (indexOfDrop > -1) { continue; }
                    //    }
                    //}

                    var validNeighbor = tiles[Graph.CalculateIndex(neighbor, data.size)];
                    neighbors.Add(validNeighbor);
                }
            }

            directions.Dispose();
            return neighbors;
        }

        private bool HasTile(Tile t)
        {
            return t.data.x < data.size.x && t.data.y < data.size.y && t.data.z < data.size.z
                && t.data.x > -1 && t.data.y > -1 && t.data.z > -1;
        }
    }
}
