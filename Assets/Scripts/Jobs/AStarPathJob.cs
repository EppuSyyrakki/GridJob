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
        [ReadOnly]
        private readonly int jumpHeight;

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
        /// <param name="includeStartInResult">Insert the starting tile in the result list</param>
        public AStarPathJob(Tile start, Tile goal, NativeArray<Tile> tiles, NativeList<Tile> result, GridData data,
            int dropDepth = 1, int jumpHeight = 0, bool includeStartInResult = false)
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
            this.jumpHeight = jumpHeight;
            includeStart = includeStartInResult;            
        }

        [BurstCompatible]
        public void Execute()
        {
            var comparer = new Heuristic(goal, data);
            var random = new Random((uint)start.data.x * (uint)start.data.y * (uint)start.data.z + 1);
            var cameFrom = new NativeArray<int>(tiles.Length, Allocator.Temp);
            var costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            var neighbors = new NativeList<Tile>(10, Allocator.Temp);
            var frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, frontierSize, comparer);            
            int examined = 0;
            bool complete = false;

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

                if (current.Equals(goal) || frontier.Count >= frontierSize) 
                {
                    complete = true;
                    break; 
                } 
                else if (frontier.Count >= frontierSize)
                {
                    complete = false;
                    break;
                }
                
                NeighborProcessor.GetNeighbors(current, data, tiles, dropDepth, jumpHeight, ref neighbors); // Filter available neighbors           

                while (neighbors.Length > 0)  // Loop through filtered neighbors
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

                neighbors.Clear();
            }

            if (!complete)
            {
                Dispose();
                return;
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

            Dispose();

            void Dispose()
            {
                frontier.Dispose();
                costSoFar.Dispose();
                cameFrom.Dispose();
                neighbors.Dispose();
            }
        }       
    }
}
