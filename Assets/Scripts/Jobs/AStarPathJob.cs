using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;

namespace Jobben
{
    [BurstCompatible]
    struct AStarPathJob : IJob
    {
        [ReadOnly]
        private readonly bool log, draw;
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

        public AStarPathJob(Tile start, Tile goal, NativeArray<Tile> tiles, NativeList<Tile> result, MapData data, 
            bool log = false, bool draw = false)
        {
            int startIndex = Graph.CalculateIndex(start, data);
            int goalIndex = Graph.CalculateIndex(goal, data);
            Assert.IsTrue(startIndex != -1 && goalIndex != -1);
            this.data = data;
            this.start = tiles[startIndex];
            this.goal = tiles[goalIndex];
            this.tiles = tiles;
            this.result = result;
            frontierSize = data.size.x * data.size.y * data.size.z / 32;
            this.log = log;
            this.draw = draw;
        }

        [BurstCompatible]
        public void Execute()
        {
            Heuristic comparer = new Heuristic(goal, data);
            Random random = new Random((uint)start.data.x * (uint)start.data.y * (uint)start.data.z + 1);
            NativeArray<int> cameFrom = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeArray<int> costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeHeap<Tile, Heuristic> frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, frontierSize, comparer);
            for (int i = 0; i < tiles.Length; i++)
            {
                costSoFar[i] = int.MaxValue;
                cameFrom[i] = -1;
            }
            Tile current = start;
            cameFrom[current.index] = current.index;
            costSoFar[current.index] = 0;
            frontier.Insert(in current);  // Frontier sorts nodes by their distance with the Heuristic comparator          

            while (frontier.Count > 0)  // Still have nodes to search.
            {
                current = frontier.Pop();
                string msg = "";

                if (frontier.Count >= frontierSize)
                {
                    if (log) { Debug.Log($"Max frontier size of {frontierSize} reached. Exiting search."); }
                    break;
                }

                // Early exit, goal found or max frontier reached
                if (current.Equals(goal)) 
                { 
                    if (log) { Debug.Log("Goal found."); } 
                    break; 
                } 

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

                    if (draw) { Debug.DrawLine(Graph.TileToWorld(current, data), Graph.TileToWorld(next, data), Color.red, 10f); }
                    if (log) { msg += $"--{next} cost {costSoFar[next.index]}"; }
                }

                if (log) { Debug.Log(msg + $". Frontier: {frontier.Count}/{frontierSize}"); msg = ""; }
                neighbors.Dispose();
            }

            int items = 0;

            while (!current.Equals(start))
            {
                if (log) { Debug.Log($"Adding {current} to result array."); }
                result.Add(tiles[current.index]);
                current = tiles[cameFrom[current.index]];
                items++;
            }

            if (log) { Debug.Log($"Adding {start} to result array."); }
            result.Add(start);
            frontier.Dispose();
            costSoFar.Dispose();
            cameFrom.Dispose();
        }

        /// <summary>
        /// Returns only valid (movable) neighbor copies from the grid. Checks for node edges and grid limits.
        /// </summary>
        private NativeList<Tile> GetNeighbors(Tile tile)
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
                if (tile.HasAnyEdge(current) && tile.IsAnyType(TileType.WalkableTypes))
                {
                    var validNeighbor = tiles[Graph.CalculateIndex(neighbor, data.size)];
                    neighbors.Add(validNeighbor);
                }
            }

            directions.Dispose();
            return neighbors;
        }
    }
}
