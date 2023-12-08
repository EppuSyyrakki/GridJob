using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GridSystem
{
	public class ShadowCast
	{
		private Tile[] tiles;
		private GridData data;
		private List<Tile> result;

		public List<Tile> Result => result;

		public ShadowCast(Tile[] tiles, GridData data)
		{
			this.tiles = tiles;
			this.data = data;
			result = new List<Tile>();
		}

		public IEnumerator Cast(Tile center, float range)
		{
			Debug.Log("Shadowcasting from " + center + " with range " + range);
            for (int txidx = 0; txidx < s_octantTransform.Length; txidx++)
            {
                // Debug.Log($"Octant: {txidx}");
                yield return CastLight(center, range, 1, 1.0f, 0.0f, s_octantTransform[txidx]);
            }
        }

		private IEnumerator CastLight(Tile origin, float range,
		int startColumn, float leftViewSlope, float rightViewSlope, OctantTransform txfrm)
		{
			// Used for distance test.
			float viewRadiusSq = range * range;

			int viewCeiling = (int)math.ceil(range);

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
					int gridX = origin.data.x + xc * txfrm.xx + zc * txfrm.xy;
					int gridZ = origin.data.z + xc * txfrm.yx + zc * txfrm.yy;

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
					float leftBlockSlope = (zc + 1f) / xc;
					float rightBlockSlope = zc / (xc + 1f);
					float nSlope = zc / xc;
					// Debug.Log($"Cell {gridX}, {gridZ} - slope {nSlope}");

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
						yield return null;
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
						&& Grid.GetIndex(new Tile(gridX, origin.data.y, gridZ), data, out int index))
					{
						Tile t = tiles[index];
						Tile toOrigin = (origin - t).Normalized;
						Color c = Color.red;

						if (t.Equals(new Tile(5, 0, 7)))
						{
							Debug.Log("BingPot!");
						}

						if (t.IsVisibleTo(toOrigin))    // both cubic and diagonal cases require LOS from the original tile
                        {
                            if (toOrigin.IsCubic)
                            {
                                // LOS is "straight" so the above visiblity check will do
                                result.Add(tiles[index]);
                                curBlocked = false;
								c = Color.green;
                            }
                            else
                            {
                                // LOS is dependant not only on "t", but also on the neighboring tiles having visibility
                                (Tile l, Tile r) = Tile.AdjacentDirections(toOrigin);

                                // Either one or both of the adjacents has visibility toward origin
                                if ((Grid.GetIndex(t + l, data, out int li) && tiles[li].IsVisibleTo(r))
                                   || (Grid.GetIndex(t + r, data, out int ri) && tiles[ri].IsVisibleTo(l)))
                                {
                                    result.Add(tiles[index]);
                                    curBlocked = false;
									c = Color.green;
                                }
                            }
                        }

                        Debug.DrawLine(Grid.TileToWorld(t, data), Grid.TileToWorld(t + toOrigin, data), c, 10f);
                        yield return new WaitForSeconds(0.016f);
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
								CastLight(origin, range, currentCol + 1,
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
	}
}