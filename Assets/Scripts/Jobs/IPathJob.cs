using Unity.Collections;
using Unity.Mathematics;

namespace GridSystem.Jobs
{
    public interface IPathJob
    {
        public NativeArray<Tile> Tiles {  get; }
        public GridData Data { get; }
        public int DropDepth { get; }
        public int JumpHeight { get; }


        /// <summary>
        /// Adds valid (movable) neighbors from tiles to referenced neighbors list. Checks for jumps, drops and climbs.
        /// </summary>
        [BurstCompatible]
        public void GetNeighbors(Tile tile, ref NativeList<Tile> neighbors)
        {
            var directions = new NativeList<Tile>(10, Allocator.Temp)
            {
                Tile.N, Tile.E, Tile.S, Tile.W, Tile.Up, Tile.Down, Tile.NE, Tile.SE, Tile.SW, Tile.NW,
            };

            for (int i = 0; i < directions.Length; i++)
            {
                Tile dir = directions[i];

                if (Grid.GetIndex(tile + dir, Data, out int index) && HasPassageTo(tile, dir))
                {
                    Tile neighbor = Tiles[index];
               
                    if (dir.data.y == 0)    // Same level neighbor
                    {                       
                        if (neighbor.walls.Down == WallType.Full) 
                        {
                            // Has a floor.
                            neighbors.Add(neighbor); 
                        }                        
                        else if (neighbor.walls.Down != WallType.None && CanDrop(neighbor, out Tile dropTarget)) 
                        {
                            // No floor, but can drop.
                            neighbors.Add(dropTarget); 
                        }                        
                        else if (i < 4 && CanClimb(tile, neighbor)) 
                        {
                            // No floor, but can climb down, add to list. Don't allow to start climbing from diagonal directions.
                            neighbors.Add(neighbor); 
                        }
                    }                   
                    else if (dir.data.y == 1)   // Above neighbor - prioritize jumping.
                    {                        
                        if (CanJump(neighbor, out Tile jumpTarget)) 
                        {
                            // Can jump to higher ground.
                            neighbors.Add(jumpTarget); 
                        }                        
                        else if (CanClimb(tile, neighbor)) 
                        {
                            // Can climb up.
                            neighbors.Add(neighbor); 
                        }
                    }                    
                    else if (CanClimb(tile, neighbor)) // Below neighbor.
                    {
                        // Dropping and starting a climb already done above, so only check climbing.
                        neighbors.Add(neighbor); 
                    }
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
        public bool HasPassageTo(Tile tile, Tile dir)
        {
            if (math.abs(dir.data.x) + math.abs(dir.data.z) > 1)
            {
                // Diagonal direction
                (Tile left, Tile right) = Tile.AdjacentDirections(dir);

                if (tile.IsMovable(left) && tile.IsMovable(right))
                {
                    // Direct walls are clear, check adjacents for their inverse walls.
                    if (Grid.GetIndex(tile + left, Data, out int leftIndex) 
                        && Grid.GetIndex(tile + right, Data, out int rightIndex))
                    {
                        return Tiles[leftIndex].IsMovable(right) && Tiles[rightIndex].IsMovable(left);
                    }
                }

                return false;
            }

            return tile.IsMovable(dir);                         
        }

        [BurstCompatible]
        private bool CanDrop(Tile neighbor, out Tile target, int depth = 1)
        {
            target = neighbor;

            // Drop is too deep, early exit
            if (depth > DropDepth) { return false; }

            if (Grid.GetIndex(neighbor + Tile.Down * depth, Data, out int index))
            {
                Tile current = Tiles[index];

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
                    return CanDrop(neighbor, out target);
                }
            }

            return false;
        }

        [BurstCompatible]
        private bool CanJump(Tile neighbor, out Tile target, int height = 1)
        {
            target = neighbor;

            // Jump is too high, or above has a floor, early exit
            if (height > JumpHeight || neighbor.walls.Down != WallType.None) { return false; }

            if (Grid.GetIndex(neighbor + Tile.Up * height, Data, out int index))
            {
                Tile current = Tiles[index];
                var directions = Tile.Directions_Lateral;

                // Loop through lateral neighbors
                for (int i = 0; i < directions.Length; i++)
                {
                    if (Grid.GetIndex(current + directions[i], Data, out int aboveNeighborIndex))
                    {
                        // Neighbor to above exists.
                        Tile aboveNeighbor = Tiles[aboveNeighborIndex];

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
                return CanJump(neighbor, out target);
            }

            return false;
        }

        private bool CanClimb(Tile tile, Tile neighbor)
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
            else if (Grid.GetIndex(neighbor + Tile.Down, Data, out int index))
            {
                // "Stepped" on empty and started ascending
                // TODO: Ensure direction from tile to neighbor is opposite to the climbable wall
                WallMask opposite = Tile.DirectionToWallMask(tile - neighbor);
                Tile below = Tiles[index];
                return (below.walls.GetMask(WallTypeMask.Climbable) & opposite) != WallMask.None;
            }

            return false;
        }
    }
}
