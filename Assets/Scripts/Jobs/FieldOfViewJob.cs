using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridSystem.Jobs
{
    [BurstCompatible]
    public struct FovJob : IJob
    {
        private const float SQRT3 = 1.7320508f;
        private const float SQRT2 = 1.41421356f;

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

        private int overlaps;
        private int total;

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
            overlaps = 0;
            total = 0;
        }

        [BurstCompatible]
        public void Execute()
        {
            Tile right = forward.Rotate(-angleWidth * 0.5f);
            Tile current = right;
            var line = new NativeList<Tile>(magnitude, Allocator.Temp);
            Linecast(ref line, center, center + current);

            for (int i = line.Length - 1; i >= 0; i--) { result.Add(line[i]); }

            for (float rotation = 1; rotation <= angleWidth; rotation++)
            {
                Tile next = right.Rotate(rotation);

                if (next.Equals(current)) { continue; } // rotation wasn't enough to get a new end tile
                
                current = next;              
                line.Clear();
                Linecast(ref line, center, center + current);

                for (int i = line.Length - 1; i >= 0; i--) { result.Add(line[i]); }                
            }

            line.Dispose();
        }

        [BurstCompatible]
        private void Linecast(ref NativeList<Tile> result, Tile a, Tile b,
            TileType tileMask = TileType.All, bool stopOnMiss = true, bool includeA = false)
        {
            Point axz = new Point(a.data.x, a.data.z), bxz = new Point(b.data.x, b.data.z);
            Point axy = new Point(a.data.x, a.data.y), bxy = new Point(b.data.x, b.data.y);
            int dist = math.max(Point.DiagonalDistance(axz, bxz), Point.DiagonalDistance(axy, bxy));
            NativeList<Point> xz = Line(axz, bxz, dist);
            NativeList<Point> xy = Line(axy, bxy, dist);

            for (int i = includeA ? 0 : 1; i <= dist; i++)
            {
                sbyte x = xz[i].q, y = xy[i].r, z = xz[i].r;

                if (!Grid.GetIndex(new Tile(x, y, z), data, out int index)) { break; }

                Tile tile = tiles[index];

                if (tile.IsAnyType(tileMask)) { result.Add(tile); }
                else if (stopOnMiss) { break; }
            }

            xz.Dispose();
            xy.Dispose();
        }

        [BurstCompatible]
        private NativeList<Point> Line(Point p0, Point p1, int dist)
        {
            NativeList<Point> points = new NativeList<Point>(dist, Allocator.Temp);

            for (int step = 0; step <= dist; step++)
            {
                float t = dist == 0 ? 0f : (float)step / dist;
                points.Add(Point.Lerp(p0, p1, t));
            }

            return points;
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