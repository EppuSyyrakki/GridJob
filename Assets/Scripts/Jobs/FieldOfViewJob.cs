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
                if (!Grid.GetIndex(new Tile(x, y, z), data, out int index)) 
                {
                    break; 
                }

                Tile next = tiles[index];
                Tile dir = (current - next).Normalized;

                if (next.IsVisibleTo(dir))
                {
                    if (dir.IsCubic)
                    {
                        line.Add(next);
                    }
                    else if (CheckAdjacents(next, dir))
                    {
                        line.Add(next);
                    }
                }
                else
                {
                    break;
                }

                current = next;
            }

            xz.Dispose();
            xy.Dispose();
        }
       
        [BurstCompatible]
        bool CheckAdjacents(Tile t, Tile dir)
        {
            var (left, right) = Tile.AdjacentDirections(dir);

            if (Grid.GetIndex(t + left, data, out int l) && Grid.GetIndex(t + right, data, out int r))
            {
                return tiles[l].IsVisibleTo(right) || tiles[r].IsVisibleTo(left);
            }

            return false;
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