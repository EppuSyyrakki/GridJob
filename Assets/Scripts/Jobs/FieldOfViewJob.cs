﻿using System;
using System.Collections.Generic;
using System.Xml;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GridSystem.Jobs
{
    [BurstCompatible]
    public struct FovJob : IJob
    {
        private const float SQRT3 = 1.7320508f;
        private const float SQRT2 = 1.41421356f;
        private const float DEG_TO_RAD = math.PI / 180;

        [ReadOnly]
        private readonly GridData data;
        [ReadOnly]
        private readonly Tile center;
        [ReadOnly]
        private readonly Tile forward;
        [ReadOnly]
        private readonly float angleWidth;
        [ReadOnly]
        private NativeArray<Tile> tiles;
        [ReadOnly]
        private bool includeCenter;
        [ReadOnly]
        private int magnitude;
        [ReadOnly]
        private float fMagnitude;

        [WriteOnly]
        private NativeHashSet<Tile> result;

        public FovJob(NativeArray<Tile> tiles, GridData data, NativeHashSet<Tile> result,
            Tile center, Tile forward, float angleWidth, bool includeCenter = false)
        {
            this.data = data;
            this.center = center;
            this.forward = forward;
            this.angleWidth = angleWidth;
            this.tiles = tiles;
            this.result = result;
            this.includeCenter = includeCenter;
            magnitude = Point.DiagonalDistance(new Point(0, 0), new Point(forward.data.x, forward.data.z));
            fMagnitude = forward.Magnitude;
        }

        [BurstCompatible]
        public void Execute()
        {
            var line = new NativeList<Tile>(Allocator.Temp);
            SweepLevel(ref line, center, forward);

            foreach (var tile in line)
            {
                result.Add(tile);
            }

            line.Dispose();

            //Shadowcast(center, magnitude);
        }

        private void SweepLevel(ref NativeList<Tile> line, Tile origin, Tile forward)
        {
            if (!Grid.GetIndex(origin, data, out _))
            {
                line.Clear();
                return;
            }

            // Calculate degrees how much to rotate to hit a new tile
            float step = math.atan(math.min(data.cellSize.x, data.cellSize.z) / fMagnitude) / DEG_TO_RAD;
            // Rotate the forward to the other edge of the cone
            Tile right = forward.Rotate(angleWidth * -0.5f);
            Tile current = origin + right;
            Sightcast(ref line, origin, current);
            TryAddToResult(ref line, magnitude);
            Tile next;

            // Starting from right, rotate left by step and linecast to each end point
            for (float rotation = 0; rotation < angleWidth; rotation += step)
            {
                next = origin + right.Rotate(rotation);

                if (next.Equals(current)) { continue; }

                if (Grid.GetIndex(next, data, out int index))
                {
                    next = tiles[index];
                }

                line.Clear();
                Sightcast(ref line, origin, next);
                TryAddToResult(ref line, magnitude);
            }
        }

        [BurstCompatible]
        private void TryAddToResult(ref NativeList<Tile> line, int currentMagnitude)
        {
            int collisions = 0;

            for (int i = line.Length - 1; i >= 0; i--)
            {
                if (!result.Add(line[i])) { collisions++; }

                if (collisions > currentMagnitude) { break; }   // largely unused, triggered only a few times per sweep
            }

            line.Clear();
        }

        [BurstCompatible]
        private void Sightcast(ref NativeList<Tile> line, Tile a, Tile b)
        {
            Point axz = new(a.data.x, a.data.z);
            Point bxz = new(b.data.x, b.data.z);
            Point axy = new(a.data.x, a.data.y);
            Point bxy = new(b.data.x, b.data.y);
            int dist = math.max(Point.DiagonalDistance(axz, bxz), Point.DiagonalDistance(axy, bxy));
            NativeList<Point> xz = new(Allocator.Temp);
            NativeList<Point> xy = new(Allocator.Temp);
            Line(ref xz, axz, bxz, dist);
            Line(ref xy, axy, bxy, dist);
            Tile current = a;

            for (int i = 1; i < xz.Length; i++)
            {
                sbyte x = xz[i].q;
                sbyte y = xy[i].r;
                sbyte z = xz[i].r;

                // out of bounds, early exit
                if (!Grid.GetIndex(new Tile(x, y, z), data, out int index)) { break; }

                Tile next = tiles[index];
                Tile dir = next - current;

                if (next.IsAnyType(TileType.BlockedTypes)) { break; }

                var dirCover = Tile.DirectionToCover(dir);

                // next is not diagonal (is cubic neighbor), can do simple check
                if (math.abs(dir.data.x) + math.abs(dir.data.y) + math.abs(dir.data.z) <= 1)
                {
                    if (current.HasNoCover(dirCover))
                    {
                        line.Add(next);
                        current = next;
                        continue;
                    }
                    else { break; }
                }

                // next is diagonal and on same level
                if (dir.data.y == 0)
                {
                    if (CheckAdjacents(current, dir) && next.HasNoCover(dirCover.Opposite()))
                    {
                        line.Add(next);
                    }
                    
                }
                else
                {
                    // next is up or down from current
                    Tile vDir = dir.data.y > 0 ? Tile.up : Tile.down;
                    Cover currentCover = dir.data.y > 0 ? Cover.Up : Cover.Down;

                    // the up or down is blocked directly, exit (OBSOLETE? CHECKED EARLIER IN CUBIC NEIGHBORS)
                    if (current.HasAnyCover(currentCover)) { break; };

                    // get the tile that neighbors both current and next
                    if (!Grid.GetIndex(current + vDir, data, out int dIndex)) { break; };

                    Tile direct = tiles[dIndex];
                    Tile dirToCover = next - direct;
                    Cover cover = Tile.DirectionToCover(dirToCover);

                    // edge case: diagonal vertically AND horizontally
                    if (math.abs(dir.data.x) + math.abs(dir.data.z) > 1)
                    {
                        // must check the adjacent lateral edges for cover
                        if (!CheckAdjacents(direct, dirToCover)) { break; }

                        line.Add(next);
                    }
                    else if (direct.HasNoCover(cover))  // just a vertical diagonal, not horizontal
                    {
                        // No cover between
                        line.Add(next);
                    }
                }

                current = next;
            }

            // Can the t + dir be seen from t?
            /// <summary> Checks </summary>
            bool CheckAdjacents(Tile t, Tile dir)
            {
                var (e1, e2) = Tile.DirectionToEdge(dir).Adjacents();

                if (t.HasNoCover((Cover)e1) || t.HasNoCover((Cover)e2))
                {
                    return true;  // At least one of the sides is open so there is LOS
                }

                return false;
            }
        }

        [BurstCompatible]
        private void Line(ref NativeList<Point> pointLine, Point p0, Point p1, int dist)
        {
            for (int step = 0; step <= dist; step++)
            {
                float t = dist == 0 ? 0f : (float)step / dist;
                pointLine.Add(Point.Lerp(p0, p1, t));
            }
        }

        [BurstCompatible]
        private void Shadowcast(Tile center, float range)
        {
            // Viewer's cell is always visible.
            //result.Add(center);

            // Cast light into cells for each of 8 octants.
            //
            // The left/right inverse slope values are initially 1 and 0, indicating a diagonal
            // and a horizontal line.  These aren't strictly correct, as the view area is supposed
            // to be based on corners, not center points.  We only really care about one side of the
            // wall at the edges of the octant though.
            //
            // NOTE: depending on the compiler, it's possible that passing the octant transform
            // values as four integers rather than an object reference would speed things up.
            // It's much tidier this way though.
            for (int txidx = 0; txidx < s_octantTransform.Length; txidx++)
            {
                Debug.Log($"Octant: {txidx}");
                CastLight(center, range, 1, 1.0f, 0.0f, s_octantTransform[txidx]);                
            }      
        }

        /// <summary>
        /// Recursively casts light into cells on a single octant. Maximum recursion depth is (Ceiling(viewRadius)).
        /// </summary>
        /// <param name="center">The player's position within the grid.</param>
        /// <param name="range">The view radius; can be a fractional value.</param>
        /// <param name="startColumn">Current column; pass 1 as initial value.</param>
        /// <param name="leftViewSlope">Slope of the left (upper) view edge; pass 1.0 as the initial value.</param>
        /// <param name="rightViewSlope">Slope of the right (lower) view edge; pass 0.0 as the initial value.</param>
        /// <param name="txfrm">Coordinate multipliers for the octant transform.</param>
        [BurstCompatible]
        private void CastLight(Tile center, float range,
                int startColumn, float leftViewSlope, float rightViewSlope, OctantTransform txfrm)
        {
            // Used for distance test.
            float viewRadiusSq = range * range;

            int viewCeiling = (int)Math.Ceiling(range);

            // Set true if the previous cell we encountered was blocked.
            bool prevWasBlocked = false;

            // As an optimization, when scanning past a block we keep track of the
            // rightmost corner (bottom-right) of the last one seen.  If the next cell
            // is empty, we can use this instead of having to compute the top-right corner
            // of the empty cell.
            float savedRightSlope = -1;

            int xDim = data.size.x;
            int zDim = data.size.z;

            // Outer loop: walk across each column, stopping when we reach the visibility limit.
            for (int currentCol = startColumn; currentCol <= viewCeiling; currentCol++)
            {
                int xc = currentCol;

                // Inner loop: walk down the current column.  We start at the top, where X==Y.
                //
                // TODO: we waste time walking across the entire column when the view area
                //   is narrow.  Experiment with computing the possible range of cells from
                //   the slopes, and iterate over that instead.
                for (int zc = currentCol; zc >= 0; zc--)
                {
                    // Translate local coordinates to grid coordinates.  For the various octants
                    // we need to invert one or both values, or swap X for Y.
                    int gridX = center.data.x + xc * txfrm.xx + zc * txfrm.xy;
                    int gridZ = center.data.z + xc * txfrm.yx + zc * txfrm.yy;

                    // Range-check the values.  This lets us avoid the slope division for blocks
                    // that are outside the grid.
                    //
                    // Note that, while we will stop at a solid column of blocks, we do always
                    // start at the top of the column, which may be outside the grid if we're (say)
                    // checking the first octant while positioned at the north edge of the map.
                    if (gridX < 0 || gridX >= xDim || gridZ < 0 || gridZ >= zDim)
                    {
                        continue;
                    }

                    // Compute slopes to corners of current block.  We use the top-left and
                    // bottom-right corners.  If we were iterating through a quadrant, rather than
                    // an octant, we'd need to flip the corners we used when we hit the midpoint.
                    //
                    // Note these values will be outside the view angles for the blocks at the
                    // ends -- left value > 1, right value < 0.
                    float leftBlockSlope = (zc + 0.5f) / (xc - 0.5f);
                    float rightBlockSlope = (zc - 0.5f) / (xc + 0.5f);
                    float nSlope = zc / xc;
                    Debug.Log($"Cell {gridX}, {gridZ} - slope {nSlope}");

                    // Check to see if the block is outside our view area.  Note that we allow
                    // a "corner hit" to make the block visible.  Changing the tests to >= / <=
                    // will reduce the number of cells visible through a corner (from a 3-wide
                    // swath to a single diagonal line), and affect how far you can see past a block
                    // as you approach it.  This is mostly a matter of personal preference.
                    if (rightBlockSlope > leftViewSlope)
                    {
                        // Block is above the left edge of our view area; skip.
                        continue;
                    }
                    else if (leftBlockSlope < rightViewSlope)
                    {
                        // Block is below the right edge of our view area; we're done.
                        break;
                    }

                    // This cell is visible, given infinite vision range.  If it's also within
                    // our finite vision range, light it up.
                    //
                    // To avoid having a single lit cell poking out N/S/E/W, use a fractional
                    // viewRadius, e.g. 8.5.
                    //
                    // TODO: we're testing the middle of the cell for visibility.  If we tested
                    //  the bottom-left corner, we could say definitively that no part of the
                    //  cell is visible, and reduce the view area as if it were a wall.  This
                    //  could reduce iteration at the corners.
                    float distanceSquared = xc * xc + zc * zc;
                    bool curBlocked = true;

                    if (distanceSquared <= viewRadiusSq 
                        && Grid.GetIndex(new Tile(gridX, center.data.y, gridZ), data, out int index))
                    {
                        Tile t = tiles[index];

                        if (t.IsAnyType(TileType.MovableTypes)) // && t.HasNoCover(txfrm.c1) && t.HasNoCover(txfrm.c2))
                        {
                            result.Add(tiles[index]);
                            curBlocked = false;
                        }                       
                    }

                    if (prevWasBlocked)
                    {
                        if (curBlocked)
                        {
                            // Still traversing a column of walls.
                            savedRightSlope = rightBlockSlope;
                        }
                        else
                        {
                            // Found the end of the column of walls.  Set the left edge of our
                            // view area to the right corner of the last wall we saw.
                            prevWasBlocked = false;
                            leftViewSlope = savedRightSlope;
                        }
                    }
                    else
                    {
                        if (curBlocked)
                        {
                            // Found a wall.  Split the view area, recursively pursuing the
                            // part to the left.  The leftmost corner of the wall we just found
                            // becomes the right boundary of the view area.
                            //
                            // If this is the first block in the column, the slope of the top-left
                            // corner will be greater than the initial view slope (1.0).  Handle
                            // that here.
                            if (leftBlockSlope <= leftViewSlope)
                            {
                                CastLight(center, range, currentCol + 1,
                                    leftViewSlope, leftBlockSlope, txfrm);
                            }

                            // Once that's done, we keep searching to the right (down the column),
                            // looking for another opening.
                            prevWasBlocked = true;
                            savedRightSlope = rightBlockSlope;
                        }
                    }
                }

                // Open areas are handled recursively, with the function continuing to search to
                // the right (down the column).  If we reach the bottom of the column without
                // finding an open cell, then the area defined by our view area is completely
                // obstructed, and we can stop working.
                if (prevWasBlocked)
                {
                    break;
                }
            }
        }

        private bool IsFree(Tile t, Cover txCover)
        {
            return t.HasNoCover(txCover);
        }

        /// <summary>
        /// Immutable class for holding coordinate transform constants.  Bulkier than a 2D
        /// array of ints, but it's self-formatting if you want to log it while debugging.
        /// </summary>
        private readonly struct OctantTransform
        {
            public readonly int xx;
            public readonly int xy;
            public readonly int yx;
            public readonly int yy;
            public readonly string desc;

            public OctantTransform(int xx, int xy, int yx, int yy, string desc)
            {
                this.xx = xx;
                this.xy = xy;
                this.yx = yx;
                this.yy = yy;
                this.desc = desc;
            }

            public override string ToString()
            {
                return desc;
            }
        }

        private static OctantTransform[] s_octantTransform = {
            new OctantTransform( 1,  0,  0,  1 , "E-NE"),   // 0 E-NE
            new OctantTransform( 0,  1,  1,  0 , "NE-N"),   // 1 NE-N
            new OctantTransform( 0, -1,  1,  0 ,"N-NW"),   // 2 N-NW
            new OctantTransform(-1,  0,  0,  1 , "NW-W"),   // 3 NW-W
            new OctantTransform(-1,  0,  0, -1 , "W-SW"),   // 4 W-SW
            new OctantTransform( 0, -1, -1,  0 , "SW-S"),   // 5 SW-S
            new OctantTransform( 0,  1, -1,  0 , "S-SE"),   // 6 S-SE
            new OctantTransform( 1,  0,  0, -1 , "SE-E"),   // 7 SE-E
        };

        private static bool PointInTriangle(Vector3 t, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 d, e;
            double w1, w2;
            d = b - a;
            e = c - a;

            if (Mathf.Approximately(e.z, 0)) { e.z = 0.0001f; } // avoid division by 0

            w1 = (e.x * (a.z - t.z) + e.z * (t.x - a.x)) / (d.x * e.z - d.z * e.x);
            w2 = (t.z - a.z - w1 * d.z) / e.z;
            return (w1 >= 0f) && (w2 >= 0.0) && ((w1 + w2) <= 1.0);
        }
    }
}