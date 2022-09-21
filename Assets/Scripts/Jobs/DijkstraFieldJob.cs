using System.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace GridJob.Jobs
{
    [BurstCompatible]
    public struct DijkstraFieldJob : IJob
    {
        [ReadOnly]
        private readonly bool log;
        [ReadOnly]
        private readonly MapData data;
        [ReadOnly]
        private readonly Tile center;
        [ReadOnly]
        private readonly int maxCost;
        [ReadOnly]
        private NativeArray<Tile> tiles;

        private Heuristic comparer;
        private Random random;

        [WriteOnly]
        private NativeList<Tile> result;

        /// <summary>
        /// Creates a job that finds and returns all tiles that cost range * data.directCost or less to enter.
        /// </summary>
        /// <param name="center">The starting tile.</param>
        /// <param name="range">Defines the max cost by multiplying with data.directCost.</param>
        /// <param name="tiles">The tiles the algorithm works with.</param>
        /// <param name="result">An array that the results will be written to. (Write only inside the job)</param>
        /// <param name="data">The struct holding information about map size.</param>
        [BurstCompatible]
        public DijkstraFieldJob(Tile center, int range, NativeArray<Tile> tiles, NativeList<Tile> result, MapData data, bool log = false)
        {
            this.center = center;
            maxCost = range * data.directCost;
            this.data = data;
            this.tiles = tiles;
            this.log = log;
            this.result = result;
            comparer = new Heuristic(center, data);
            random = new Random(((uint)center.data.x * (uint)center.data.y * (uint)center.data.z + 1) << 4);
        }

        [BurstCompatible]
        public void Execute()
        {
            NativeArray<int> costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeList<int> closed = new NativeList<int>(maxCost * maxCost, Allocator.Temp);
            Tile begin = tiles[Graph.CalculateIndex(center, data)];
            var frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, maxCost * maxCost, comparer);
            for (int i = 0; i < tiles.Length; i++) { costSoFar[i] =  int.MaxValue; }

            Tile current = begin;
            costSoFar[current.index] = 0;
            frontier.Insert(current);          
            string msg = "";

            while (frontier.Count > 0)
            {
                current = frontier.Pop();
                result.Add(current);
                NativeList<Tile> neighbors = GetNeighbors(current);
                if (log) { msg += ("DFJ---Examining: " + current + " with " + neighbors.Length + " neighbors: "); }

                while (neighbors.Length > 0)  // Loop through all available neighbors
                {                   
                    int neighborIndex = random.NextInt(0, neighbors.Length);                 
                    Tile next = neighbors[neighborIndex];
                    int costToNext = costSoFar[current.index] + Graph.Cost(next - current, data);

                    if (costToNext <= maxCost && !closed.Contains(next.index))
                    {
                        // result.Add(next);
                        frontier.Insert(next);
                        costSoFar[next.index] = costToNext;
                    }

                    closed.Add(next.index);
                    neighbors.RemoveAt(neighborIndex);
                    if (log) { msg += $"--{next} cost {costSoFar[next.index]}/{maxCost}"; }
                }
               
                neighbors.Dispose();
                if (log) { Debug.Log(msg); }
            }
        }

        /// <summary>
        /// Returns only valid (movable) neighbor copies from the grid. Checks for node edges and grid limits.
        /// </summary>
        [BurstCompatible]
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

                // Manage with just checking the edge and relying on setting them up accurately.
                if (tile.HasAnyEdge(current))
                {
                    var validNeighbor = tiles[Graph.CalculateIndex(neighbor, data.size)];

                    if (validNeighbor.IsAnyType(TileType.WalkableTypes))
                    {
                        neighbors.Add(validNeighbor);
                    }                   
                }
            }

            directions.Dispose();
            return neighbors;
        }
    }
}
