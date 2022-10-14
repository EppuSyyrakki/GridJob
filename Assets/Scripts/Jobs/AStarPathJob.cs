using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridSystem.Jobs
{
    [BurstCompatible]
    struct AStarPathJob : IJob
    {
        [ReadOnly]
        private readonly bool includeStart;
        [ReadOnly]
        private readonly GridData data;
        [ReadOnly]
        private readonly Tile start;
        [ReadOnly]
        private readonly Tile goal;
        [ReadOnly]
        private readonly int frontierSize;
        [ReadOnly]
        private NativeArray<Tile> tiles;
        [ReadOnly]
        private readonly int dropDepth;

        [WriteOnly]
        private NativeList<Tile> result;

        /// <summary>
        /// Creates a pathfinding job using A*. 
        /// </summary>
        /// <param name="start">Starting tile</param>
        /// <param name="goal">Target tile</param>
        /// <param name="tiles">Map tiles as a flat array</param>
        /// <param name="result">List that the resulting path is written in</param>
        /// <param name="data">Struct holding map information</param>
        /// <param name="log">Log search to console</param>
        /// <param name="draw">Draw debug lines for search visualization</param>
        /// <param name="includeStartInResult">Insert the starting tile in the result list</param>
        public AStarPathJob(Tile start, Tile goal, NativeArray<Tile> tiles, NativeList<Tile> result, GridData data,
            int dropDepth = 1, bool includeStartInResult = false)
        {
            int startIndex = Grid.GetIndex(start, data);
            int goalIndex = Grid.GetIndex(goal, data);
            this.data = data;
            this.start = tiles[startIndex];
            this.goal = tiles[goalIndex];
            this.tiles = tiles;
            this.result = result;
            frontierSize = math.max(data.size.x, math.max((int)data.size.y, data.size.z)) * 6;
            this.dropDepth = dropDepth;
            includeStart = includeStartInResult;            
        }

        [BurstCompatible]
        public void Execute()
        {
            Heuristic comparer = new Heuristic(goal, data);
            Random random = new Random((uint)start.data.x * (uint)start.data.y * (uint)start.data.z + 1);
            NativeArray<int> cameFrom = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeArray<int> costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            NativeHeap<Tile, Heuristic> frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, frontierSize, comparer);
            int examined = 0;
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

                if (current.Equals(goal) || frontier.Count >= frontierSize) { break; } // Early exit - frontier full or goal reached

                NativeList<Tile> neighbors = GetNeighbors(current); // Filter available neighbors               

                while (neighbors.Length > 0)  // Loop through all available neighbors
                {
                    int neighborIndex = random.NextInt(0, neighbors.Length);
                    Tile next = neighbors[neighborIndex];
                    int costToNeighbor = Grid.Cost(next - current, data);
                    int newCost = costSoFar[current.index] + costToNeighbor;
                    examined++;

                    if (newCost < costSoFar[next.index])
                    {
                        // We haven't been through this neighbor yet, or have found a shorter path
                        costSoFar[next.index] = newCost;
                        cameFrom[next.index] = current.index;
                        frontier.Insert(in next);
                    }

                    neighbors.RemoveAt(neighborIndex);
                }

                neighbors.Dispose();
            }

            while (!current.Equals(start))
            {
                result.Add(tiles[current.index]);
                current = tiles[cameFrom[current.index]];
            }

            if (includeStart)
            {
                result.Add(start);
            }
            
            frontier.Dispose();
            costSoFar.Dispose();
            cameFrom.Dispose();
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
