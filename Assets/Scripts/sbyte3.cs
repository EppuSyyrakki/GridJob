using System;
using UnityEngine;

namespace Jobben
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
                sbyte nX = x, nY = y, nZ = z;

                if (nX < -1) { nX = -1; }
                else if (nX > 1) { nX = 1; }

                if (nY < -1) { nY = -1; }
                else if (nY > 1) { nY = 1; }

                if (nZ < -1) { nZ = -1; }
                else if (nZ > 1) { nZ = 1; }

                return new sbyte3(nX, nY, nZ);
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

        public static sbyte3 operator +(sbyte3 a, sbyte3 b)
        {
            return new sbyte3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static sbyte3 operator -(sbyte3 a, sbyte3 b)
        {
            return new sbyte3(a.x - b.x, a.y - b.y, a.z - b.z);
        }
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