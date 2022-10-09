using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridJob.Jobs
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
        
        [WriteOnly]
        private NativeList<Tile> result;

        public FovJob(NativeArray<Tile> tiles, GridData data, NativeList<Tile> result, 
            Tile center, Tile forward, float angleWidth)
        {
            this.data = data;
            this.center = center;
            this.forward = forward;
            this.angleWidth = angleWidth;
            this.tiles = tiles;
            this.result = result;
        }

        [BurstCompatible]
        public void Execute()
        {
            NativeList<Tile> line = Line(center, center + forward);
            result.AddRange(line);
            line.Dispose();
        }

        [BurstCompatible]
        private NativeList<Tile> Line(Tile a, Tile b)
        {
            int dist = DiagonalDistance(a, b);
            var points = new NativeList<Tile>(dist, Allocator.Temp);

            for (int step = 0; step <= dist; step++)
            {
                float t = dist == 0 ? 0 : (float)step / dist;
                Tile tile = Lerp(a, b, t);
                
                if (!Grid.GetIndex(tile, data, out int index)) { break; }
                
                tile = tiles[index];
                    
                if (tile.IsAnyType(TileType.BlockedTypes)) { break; }
                  
                points.Add(in tile);               
            }

            return points;
        }

        [BurstCompatible]
        private int DiagonalDistance(in Tile a, in Tile b)
        {
            float3 f = (b.data - a.data).Abs;
            return (int)math.sqrt(f.x * f.x + f.y * f.y + f.z * f.z) - 1;
        }

        [BurstCompatible]
        private Tile Lerp(in Tile a, in Tile b, in float t)
        {
            return new Tile(
                math.round(Lerp(in a.data.x, in b.data.x, in t)),
                math.round(Lerp(in a.data.y, in b.data.y, in t)),
                math.round(Lerp(in a.data.z, in b.data.z, in t))
                );
        }

        [BurstCompatible]
        private float Lerp(in sbyte a, in sbyte b, in float t)
        {
            return a + t * (b - a);
        }
    }
}