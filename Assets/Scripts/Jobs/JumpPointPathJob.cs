using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GridJob.Jobs
{
    /// <summary>
    /// CURRENTLY NOT WORKING. Jump search tries to do something with the neighbors array but the logic is flawed.
    /// </summary>
    [BurstCompatible]
    public struct JumpPointPathJob : IJob
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

        private Heuristic comparer;
        private Random random;

        /// <summary>
        /// Creates a job that finds and returns shortest path to param goal. Faster than A*.
        /// </summary>
        /// <param name="start">The starting tile.</param>
        /// <param name="goal">The target tile.</param>
        /// <param name="tiles">The tiles the algorithm works with. Should be pruned to relevant tiles.</param>
        /// <param name="result">An array that the results will be written to. (Write only inside the job)</param>
        [BurstCompatible]
        public JumpPointPathJob(Tile start, Tile goal, NativeArray<Tile> tiles, NativeList<Tile> result, MapData data,
            bool log = false, bool draw = false)
        {
            this.tiles = tiles;
            this.data = data;
            this.start = tiles[Graph.CalculateIndex(start, data.size)];
            this.goal = tiles[Graph.CalculateIndex(goal, data.size)]; ;           
            this.result = result;         
            this.log = log;
            this.draw = draw;
            frontierSize = data.size.x * data.size.y * data.size.z / 24;
            comparer = new Heuristic(goal, data);
            random = new Random(((uint)start.data.x * (uint)start.data.y * (uint)start.data.z + 1) << 4);
        }

        [BurstCompatible]
        public void Execute()
        {
            var cameFrom = new NativeArray<int>(tiles.Length, Allocator.Temp);
            var costSoFar = new NativeArray<int>(tiles.Length, Allocator.Temp);
            Tile begin = tiles[Graph.CalculateIndex(start, data.size)];
            Tile target = tiles[Graph.CalculateIndex(goal, data.size)];
            var frontier = new NativeHeap<Tile, Heuristic>(Allocator.Temp, frontierSize, comparer);

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

                NativeList<Tile> jumpPoints = GetJumpPoints(current); // Filter available jump points               
                if (log) { msg += ("JPS--Examining: " + current + " with " + jumpPoints.Length + " jump points: "); }

                while (jumpPoints.Length > 0)  // Loop through all available neighbors
                {
                    int jumpIndex = random.NextInt(0, jumpPoints.Length);
                    Tile jump = jumpPoints[jumpIndex];
                    int costTojump = Heuristic.Manhattan(current, jump, data);
                    int newCost = costSoFar[current.index] + costTojump;

                    if (newCost < costSoFar[jump.index])
                    {
                        // We haven't been through this neighbor yet, or have found a shorter path
                        costSoFar[jump.index] = newCost;
                        cameFrom[jump.index] = current.index;
                        frontier.Insert(in jump);
                    }

                    jumpPoints.RemoveAt(jumpIndex);

                    if (draw)  { Debug.DrawLine(Graph.TileToWorld(current, data), Graph.TileToWorld(jump, data), Color.red, 5f); }
                    if (log) { msg += $"--{jump} cost {costSoFar[jump.index]}"; }       
                }

                if (log) { Debug.Log(msg + $". Frontier: {frontier.Count}/{frontierSize}"); msg = ""; }
                jumpPoints.Dispose();
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
        [BurstCompatible]
        private NativeList<Tile> GetJumpPoints(Tile current, int dropDepth = 2)
        {
            var directions = new NativeList<Tile>(10, Allocator.Temp)
            {   // These should be in the same order as the Edges enum for the bit-shift looping to work
                Tile.n, Tile.e, Tile.s, Tile.w, Tile.ne, Tile.se, Tile.sw, Tile.nw, Tile.up, Tile.down 
            };  

            var neighbors = new NativeList<Tile>(10, Allocator.Temp);

            for (int i = 0; i < directions.Length; i++)
            {
                Edge edge = (Edge)(1 << i);

                if (current.HasAnyEdge(edge))
                {
                    neighbors.Add(tiles[Graph.CalculateIndex(current + directions[i], data)]);
                }
            }
           
            FindJumps(in current, ref neighbors);
            directions.Dispose();
            return neighbors;
        }

        private void FindJumps(in Tile current, ref NativeList<Tile> neighbors)
        {
            for (int i = neighbors.Length - 1; i >= 0 ; i--)
            {
                Tile jumpPoint = Jump(current, Tile.DirectionToEdge(neighbors[i] - current));

                if (jumpPoint.Equals(Tile.MaxValue)) 
                {
                    neighbors.RemoveAt(i);
                    continue; 
                }  // No jump point found in direction

                neighbors[i] = jumpPoint;
            }
        }

        private Tile Jump(in Tile current, Edge direction)
        {
            if (!Graph.CalculateIndex(current + direction, data, out int index)) { return Tile.MaxValue; }

            // Has edge to that direction
            Tile tile = tiles[index];
            
            if (tile.Equals(goal)) { return tile; }

            // TODO: Check if possible Jump Point all directions, if found, return it, else, keep jumping same dir
            if (IsJumpPoint(tile, direction)) { return tile; }

            return Jump(tile, direction);  // Some recursive magic here
        }

        private bool IsJumpPoint(Tile tile, Edge dir)
        {
            if (dir.IsAnyOf(Edge.AllDirect))
            {
                return JumpDirect(tile, dir);
            }
            else if (dir.IsAnyOf(Edge.AllDiagonal))
            {
                return JumpDiagonal(tile, dir);
            }
            else if (dir.IsAnyOf(Edge.Up | Edge.Down))
            {
                return JumpVertical(tile, dir);
            }

            if (log) { Debug.LogWarning($"Couldn't find a jump point from {tile} to {dir}"); }
            return false;
        }

        private bool JumpDirect(Tile tile, Edge dir)
        {
            Edge foundBlocked = Edge.None;

            // Direct movement needs to have a free tile diagonally beyond the blockage found to make it a jump tile.
            if (dir.IsAnyOf(Edge.North | Edge.South)) // moving N or S, check E and W for non-edges
            {
                if (!tile.HasAnyEdge(Edge.East)) { foundBlocked = Edge.East; }  // Save dir where blockage was found
                else if (!tile.HasAnyEdge(Edge.West)) { foundBlocked = Edge.West; }
            }
            else if (dir.IsAnyOf(Edge.East | Edge.West))  // moving E or W, check N and S for non-edges
            {
                if (!tile.HasAnyEdge(Edge.North)) { foundBlocked = Edge.North; } // Save dir where blockage was found
                else if (!tile.HasAnyEdge(Edge.South)) { foundBlocked = Edge.South; }
            }

            // Check for a free tile in tile + saved blockage + direction
            if (!Graph.CalculateIndex(tile + foundBlocked + dir, data, out int index)) { return false; }
            else if (tiles[index].IsAnyType(TileType.WalkableTypes)) { return true; }

            return false;
        }

        private bool JumpDiagonal(Tile tile, Edge dir)
        {
            Edge foundBlocked = Edge.None;

            // For diagonals, check only the "parts" of the direction we are moving in (ie moving nortwest, check N and W)
            if (dir.IsAnyOf(Edge.NorthEast | Edge.NorthWest)) { foundBlocked |= Edge.North; }
            if (dir.IsAnyOf(Edge.SouthWest | Edge.SouthEast)) { foundBlocked |= Edge.South; }
            if (dir.IsAnyOf(Edge.NorthEast | Edge.SouthEast)) { foundBlocked |= Edge.East; }
            if (dir.IsAnyOf(Edge.NorthWest | Edge.SouthWest)) { foundBlocked |= Edge.West; }

            if (!tile.HasAnyEdge(foundBlocked)) { return true; }

            return false;
        }

        private bool JumpVertical(Tile tile, Edge dir)
        {
            return false;
        }

        /*  Jump point rules for checking for blocked tiles
         * moving   check
         *          N   E   S   W
         * North        x       x
         * East     x       x
         * South        x       x
         * West     x       x
         * N.East   x   x
         * S.East       x   x
         * S.West           x   x
         * N.West   x           x
         */

        //private bool JumpPoint(Tile tile, Edge dir)
        //{
        //    Edge edgesToCheck = Edge.None, shouldBeEmpty = Edge.None;

        //    if (dir.IsAnyOf(Edge.East | Edge.West | Edge.NorthEast | Edge.NorthWest)) { edgesToCheck |= Edge.North; }
        //    if (dir.IsAnyOf(Edge.North | Edge.South | Edge.NorthEast | Edge.SouthEast)) { edgesToCheck |= Edge.East; }
        //    if (dir.IsAnyOf(Edge.East | Edge.West | Edge.SouthEast | Edge.SouthWest)) { edgesToCheck |= Edge.South; }
        //    if (dir.IsAnyOf(Edge.North | Edge.South | Edge.SouthWest | Edge.SouthEast)) { edgesToCheck |= Edge.West; }

        //    if (dir.IsAnyOf(Edge.AllDirect)) { shouldBeEmpty = dir; }
        //    else if (!tile.HasAnyEdge(edgesToCheck)) { return true; }   // Diagonal movement doesn't need the additional empty check

        //    if (!tile.HasAnyEdge(edgesToCheck)) // direct movement, check for the additional empty tile
        //    { 
        //        // separate the edges
        //    }
        //}
    }
}
