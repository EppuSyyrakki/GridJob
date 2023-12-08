using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GridSystem
{
    /// <summary>
    /// A struct to use as node coordinate. Reduces node size considerably compared to int3 (24 vs 96 bits)
    /// </summary>
    [Serializable]
    public struct sbyte3
    {
        [SerializeField]
        public sbyte x, y, z;

        public sbyte3 Normalized
        {
            get
            {
                float m = Magnitude;
                return new(math.round(x / m), math.round(y / m), math.round(z / m));
                //sbyte nX = x, nY = y, nZ = z; // This way doesn't really handle diagonals well.

                //if (nX < -1) { nX = -1; }
                //else if (nX > 1) { nX = 1; }

                //if (nY < -1) { nY = -1; }
                //else if (nY > 1) { nY = 1; }

                //if (nZ < -1) { nZ = -1; }
                //else if (nZ > 1) { nZ = 1; }

                //return new sbyte3(nX, nY, nZ);
            }
        }

        [BurstCompatible]
        public float Magnitude 
        { get
            {
                return math.sqrt(x * x + y * y + z * z);
            } 
        }

        public sbyte3 Abs
        {
            get
            {
                sbyte aX = x, aY = y, aZ = z;

                if (aX < 0) { aX *= -1; }
                if (aY < 0) { aY *= -1; }
                if (aZ < 0) { aZ *= -1; }

                return new sbyte3(aX, aY, aZ);
            }
        }

        public sbyte3(int x, int y, int z) { this.x = (sbyte)x; this.y = (sbyte)y; this.z = (sbyte)z; }
        public sbyte3(float x, float y, float z) { this.x = (sbyte)x; this.y = (sbyte)y; this.z = (sbyte)z; }

        public static sbyte3 operator +(sbyte3 a, sbyte3 b) { return new sbyte3(a.x + b.x, a.y + b.y, a.z + b.z); }

        public static sbyte3 operator -(sbyte3 a, sbyte3 b) { return new sbyte3(a.x - b.x, a.y - b.y, a.z - b.z);  }

        public static implicit operator float3(sbyte3 s) { return new float3(s.x, s.y, s.z); }

        public static implicit operator sbyte3(float3 f) { return new sbyte3((sbyte)f.x, (sbyte)f.y, (sbyte)f.z); }

        public static implicit operator int3(sbyte3 s) { return new int3(s.x, s.y, s.z); }
        public override int GetHashCode() { return (int)x | (y << 8) | (z << 16); }
    }

    /// <summary>
    /// Struct helper to use as the size in the MapData struct.
    /// </summary>
    [Serializable]
    public struct byte3
    {
        [SerializeField]
        public byte x, y, z;
    }
}