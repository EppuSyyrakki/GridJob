using Unity.Collections;
using Unity.Mathematics;

namespace GridSystem.Jobs
{
    public class NeighborProcessor
    {
        /// <summary>
        /// Adds valid (movable) neighbors from tiles to referenced neighbors list. Checks for jumps, drops and climbs.
        /// </summary>
        [BurstCompatible]
        public static void GetNeighbors(Tile tile, in GridData data, in NativeArray<Tile> tiles, int dropDepth, int jumpHeight, ref NativeList<Tile> neighbors)
        {
            var directions = new NativeList<Tile>(10, Allocator.Temp)
            {
                Tile.N, Tile.E, Tile.S, Tile.W, Tile.Up, Tile.Down, Tile.NE, Tile.SE, Tile.SW, Tile.NW,
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Tile dir = directions[i];

                if (Grid.GetIndex(tile + dir, data, out int index) && HasPassageTo(tile, dir, data, tiles))
                {
                    Tile neighbor = tiles[index];

                    if (neighbor.occupied) { continue; }

                    // Same level neighbor
                    if (dir.data.y == 0)
                    {
                        // Has a floor, add to list
                        if (neighbor.walls.Down == WallType.Full) 
                        { 
                            neighbors.Add(neighbor); 
                        }
                        // Doesn't have a floor, but can drop, add drop target to list. Prevent dropping when no floor (is climbing)
                        else if (neighbor.walls.Down != WallType.None 
                            && CanDrop(neighbor, out Tile dropTarget, in data, in tiles, dropDepth)) { neighbors.Add(dropTarget); }
                        // Doesn't have a floor, but can climb down, add to list. Don't allow to start climbing from diagonal directions
                        else if (i < 4 && CanClimb(tile, neighbor, data, tiles)) { neighbors.Add(neighbor); }
                    }
                    // Above neighbor - prioritize jumping
                    else if (dir.data.y == 1)
                    {
                        // Can jump to higher ground, add jump target to list
                        if (CanJump(neighbor, out Tile jumpTarget, in data, tiles, jumpHeight)) { neighbors.Add(jumpTarget); }
                        // Can climb up, add to list
                        else if (CanClimb(tile, neighbor, data, tiles)) { neighbors.Add(neighbor); }
                    }
                    // Below neighbor - drop already done above, so only check climbing
                    else if (CanClimb(tile, neighbor, data, tiles)) { neighbors.Add(neighbor); }
                }
            }

            directions.Dispose();
        }

        /// <summary>
        /// Checks if this tile has walls in a direction. For diagonal directions, both adjacent walls must be free. 
        /// Does not account for climbing/dropping.
        /// </summary>
        /// <param name="dir">The direction that is checked. Coordinates must be -1..1</param>
        /// <returns>True if tile has passage to direction, false if blocked</returns>
        [BurstCompatible]
        public static bool HasPassageTo(Tile tile, Tile dir, GridData data, in NativeArray<Tile> tiles)
        {
            if (math.abs(dir.data.x) + math.abs(dir.data.z) > 1)
            {
                // Diagonal direction
                (Tile left, Tile right) = Tile.AdjacentDirections(dir);

                if (tile.IsMovable(left) && tile.IsMovable(right))
                {
                    // Direct walls are clear
                    if (Grid.GetIndex(tile + left, data, out int leftIndex) && Grid.GetIndex(tile + right, data, out int rightIndex))
                    {
                        return tiles[leftIndex].IsMovable(right) && tiles[rightIndex].IsMovable(left);
                    }
                }

                return false;
            }

            return tile.IsMovable(dir);                         
        }

        [BurstCompatible]
        private static bool CanDrop(Tile neighbor, out Tile target, in GridData data, in NativeArray<Tile> tiles, int dropDepth, int depth = 1)
        {
            target = neighbor;

            // Drop is too deep, early exit
            if (depth > dropDepth) { return false; }

            if (Grid.GetIndex(neighbor + Tile.Down * depth, data, out int index))
            {
                Tile current = tiles[index];

                // Below has a floor
                if (current.walls.Down == WallType.Full)
                {
                    target = current;
                    return true;
                }
                else
                {
                    // Increase search depth and try again
                    depth++;
                    return CanDrop(neighbor, out target, data, tiles, dropDepth, depth);
                }
            }

            return false;
        }

        [BurstCompatible]
        private static bool CanJump(Tile neighbor, out Tile target, in GridData data, in NativeArray<Tile> tiles, int jumpHeight, int height = 1)
        {
            target = neighbor;

            // Jump is too high, or above has a floor, early exit
            if (height > jumpHeight || neighbor.walls.Down != WallType.None) { return false; }

            if (Grid.GetIndex(neighbor + Tile.Up * height, data, out int index))
            {
                Tile current = tiles[index];
                var directions = Tile.Directions_Lateral;

                // Loop through lateral neighbors
                for (int i = 0; i < directions.Length; i++)
                {
                    if (Grid.GetIndex(current + directions[i], data, out int aboveNeighborIndex))
                    {
                        // Neighbor to above exists.
                        Tile aboveNeighbor = tiles[aboveNeighborIndex];

                        if (aboveNeighbor.walls.Down == WallType.Full)
                        {
                            // That neighbor to above has a floor. Success.
                            target = aboveNeighbor;
                            return true;
                        }
                    }
                }

                // No platforms found to jump on. Increase search height and try again
                height++;
                return CanJump(neighbor, out target, data, tiles, jumpHeight, height);
            }

            return false;
        }

        private static bool CanClimb(Tile tile, Tile neighbor, GridData data, in NativeArray<Tile> tiles)
        {
            if (neighbor.data.y > tile.data.y)
            {
                // Ascending      
                return tile.walls.GetMask(WallTypeMask.Climbable) > 0;
            }
            else if (neighbor.data.y < tile.data.y)
            {
                // Descending
                return neighbor.walls.GetMask(WallTypeMask.Climbable) > 0;
            }
            else if (Grid.GetIndex(neighbor + Tile.Down, data, out int index))
            {
                // "Stepped" on empty and started ascending
                // TODO: Ensure direction from tile to neighbor is opposite to the climbable wall
                WallMask opposite = Tile.DirectionToWallMask(tile - neighbor);
                Tile below = tiles[index];
                return (below.walls.GetMask(WallTypeMask.Climbable) & opposite) != WallMask.None;
            }

            return false;
        }
    }
}
