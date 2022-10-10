using Unity.Jobs;
using Unity.Collections;

namespace GridSystem.Jobs
{
    [BurstCompatible]
    public struct DijkstraFieldJob : IJob
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
        private int dropDepth;

        private Heuristic comparer;

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
        /// <param name="includeStart">Include the center tile in the result list.</param>
        [BurstCompatible]
        public DijkstraFieldJob(Tile center, int range, NativeArray<Tile> tiles, NativeList<Tile> result, GridData data,
            int dropDepth = 1, bool includeStart = false)
        {
            this.center = center;
            maxCost = range * data.directCost;
            this.data = data;
            this.tiles = tiles;
            this.dropDepth = dropDepth;
            this.includeStart = includeStart;
            this.result = result;
            comparer = new Heuristic(center, data);
        }

        [BurstCompatible]
        public void Execute()
        {
            NativeArray<int> costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeList<int> result = new NativeList<int>(maxCost * data.directCost, Allocator.Temp);
            var frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, maxCost * maxCost, comparer);
            int examined = 0;
            int items = 0;
            for (int i = 0; i < tiles.Length; i++) { costSoFar[i] =  int.MaxValue; }

            Tile begin = tiles[Grid.GetIndex(center, data)];
            costSoFar[begin.index] = 0;
            frontier.Insert(begin);
            if (includeStart) { result.Add(begin.index); }

            while (frontier.Count > 0)
            {
                var current = frontier.Pop();
                NativeList<Tile> neighbors = GetNeighbors(current);

                for (int i = 0; i < neighbors.Length; i++)  // Loop through all available neighbors
                {                         
                    Tile next = neighbors[i];
                    int costToNext = costSoFar[current.index] + Grid.Cost(next - current, data);
                    examined++;

                    if (costToNext <= maxCost && !result.Contains(next.index))
                    {
                        result.Add(next.index);
                        items++;
                        frontier.Insert(in next);
                        costSoFar[next.index] = costToNext;
                    }
                }

                neighbors.Dispose();
            }

            for (int i = 0; i < result.Length; i++)
            {
                int index = result[i];
                this.result.Add(tiles[index]);
            }
        }

        /// <summary>
        /// Returns only valid (movable) neighbor copies from the grid. Checks for node edges and grid limits.
        /// </summary>
        [BurstCompatible]
        private NativeList<Tile> GetNeighbors(Tile tile)
        {
            var directions = new NativeList<Tile>(10, Allocator.Temp)
            {   // These should be in the same order as the Edges enum for the bit-shift looping to work
                Tile.n, Tile.e, Tile.s, Tile.w, Tile.up, Tile.down, Tile.ne, Tile.se, Tile.sw, Tile.nw,
            };

            var neighbors = new NativeList<Tile>(10, Allocator.Temp);

            for (int i = 0; i < directions.Length; i++)
            {
                Edge current = (Edge)(1 << i);

                if (tile.HasPassageTo(current))
                {
                    var neighbor = tiles[Grid.GetIndex(tile + directions[i], data.size)];

                    if (neighbor.IsAnyType(TileType.Occupied)) { continue; }
                    else if (neighbor.IsAnyType(TileType.Jump) && !CanDrop(neighbor)) { continue; }

                    neighbors.Add(neighbor);
                }
            }

            directions.Dispose();
            return neighbors;
        }

        [BurstCompatible]
        private bool CanDrop(Tile t)
        {
            for (int i = 0; i < dropDepth; i++)
            {
                if (Grid.GetIndex(t + Tile.down, data, out int belowIndex))
                {
                    t = tiles[belowIndex];

                    if (t.IsAnyType(TileType.Jump)) { return false; }
                }
            }

            return true;
        }
    }
}
