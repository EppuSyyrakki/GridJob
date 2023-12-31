﻿using Unity.Collections;
using Unity.Jobs;

namespace GridSystem.Jobs
{
    [BurstCompatible]
    public struct DijkstraFieldJob : IJob, IPathJob
    {
        [ReadOnly]
        private readonly bool includeStart;
        [ReadOnly]
        private readonly GridData data;
        [ReadOnly]
        private readonly Tile center;
        [ReadOnly]
        private readonly int maxCost;
        [ReadOnly]
        private NativeArray<Tile> tiles;
        [ReadOnly]
        private readonly int dropDepth;
        [ReadOnly]
        private readonly int jumpHeight;

        [WriteOnly]
        private NativeList<Tile> result;

        public readonly NativeArray<Tile> Tiles => tiles;
        public readonly GridData Data => data;
        public readonly int DropDepth => dropDepth;
        public readonly int JumpHeight => jumpHeight;

        /// <summary>
        /// Creates a job that finds and returns all tiles that cost range * data.directCost or less to enter.
        /// </summary>
        /// <param name="center">The starting tile.</param>
        /// <param name="range">Defines the max cost by multiplying with data.directCost.</param>
        /// <param name="tiles">The tiles the algorithm works with.</param>
        /// <param name="result">An array that the results will be written to. (Write only inside the job)</param>
        /// <param name="data">The struct holding information about map size.</param>
        /// <param name="includeStart">Include the center tile in the result list.</param>
        [BurstCompatible]
        public DijkstraFieldJob(Tile center, int range, NativeArray<Tile> tiles, NativeList<Tile> result, GridData data,
            int dropDepth = 1, int jumpHeight = 0, bool includeStart = false)
        {
            this.center = center;
            maxCost = range * data.directCost;
            this.data = data;
            this.tiles = tiles;
            this.dropDepth = dropDepth;
            this.includeStart = includeStart;
            this.result = result;  
            this.jumpHeight = jumpHeight;
        }

        [BurstCompatible]
        public void Execute()
        {
            var comparer = new Heuristic(center, data);
            var costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            var result = new NativeList<int>(maxCost * data.directCost, Allocator.Temp);
            var neighbors = new NativeList<Tile>(10, Allocator.Temp);
            var frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, maxCost * maxCost, comparer);
            var iPath = this as IPathJob;
            int examined = 0;
            int items = 0;
            for (int i = 0; i < tiles.Length; i++) { costSoFar[i] =  int.MaxValue; }

            costSoFar[center.index] = 0;
            frontier.Insert(center);
            if (includeStart) { result.Add(center.index); }

            while (frontier.Count > 0)
            {
                Tile current = frontier.Pop();
                iPath.GetNeighbors(current, ref neighbors);

                for (int i = 0; i < neighbors.Length; i++)  // Loop through all available neighbors
                {                         
                    Tile next = neighbors[i];
                    int costToNext = costSoFar[current.index] + Grid.Cost(next - current, data);
                    examined++;

                    if (costToNext <= maxCost && !result.Contains(next.index))
                    {
                        // TODO: What if we found a shorter path to one already calculated?
                        result.Add(next.index);
                        items++;
                        frontier.Insert(next);
                        costSoFar[next.index] = costToNext;
                    }
                }

                neighbors.Clear();
            }

            for (int i = 0; i < result.Length; i++)
            {
                int index = result[i];
                this.result.Add(tiles[index]);
            }

            costSoFar.Dispose();
            neighbors.Dispose();
            frontier.Dispose();
        }            
    }
}
