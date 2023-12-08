using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace GridSystem
{
    [Serializable]
    public struct Tile : IEquatable<Tile>
    {
        private const float DEG_TO_RAD = math.PI / 180;

        [SerializeField]
        public sbyte3 data;
        [SerializeField]
        public Walls walls;
        [SerializeField]
        public int index;

        #region Properties

        [BurstCompatible]
        public Tile Normalized => new(data.Normalized, walls, index);
        [BurstCompatible]
        public readonly bool IsCubic => math.abs(data.x) + math.abs(data.y) + math.abs(data.z) <= 1;
        [BurstCompatible]
        public readonly bool IsCubicHorizontal => math.abs(data.x) + math.abs(data.z) <= 1;
        [BurstCompatible]
        public float Magnitude => data.Magnitude;

        #endregion

        #region Constructors
        public Tile(int x, int y, int z, Walls walls = new Walls(), int index = -1)
        {
            data = new sbyte3(x, y, z);
            this.walls = walls;
            this.index = index;
        }

        public Tile(Vector3 v, Walls walls = new Walls(), int index = -1)
        {
            data = new sbyte3((sbyte)math.round(v.x), (sbyte)math.round(v.y), (sbyte)math.round(v.z));
            this.walls = walls;
            this.index = index;
        }

        public Tile(float x, float y, float z, Walls walls = new Walls(), int index = -1)
        {
            data = new sbyte3((sbyte)math.round(x), (sbyte)math.round(y), (sbyte)math.round(z));
            this.walls = walls;
            this.index = index;
        }

        public Tile(sbyte x, sbyte y, sbyte z, Walls walls = new Walls(), int index = -1)
        {
            data = new sbyte3(x, y, z);
            this.walls = walls;
            this.index = index;
        }

        public Tile(sbyte3 data, Walls walls = new Walls(), int index = -1)
        {
            this.data = data;
            this.walls = walls;
            this.index = index;
        }
        #endregion

        #region Helpers and operators       
        /// <summary>
        /// Rotates a tile vector in 2D. Positive degrees rotate counter-clockwise.
        /// </summary>
        [BurstCompatible]
        public readonly Tile Rotate(float degrees)
        {
            if (degrees == 0) { return this; }

            degrees %= 360;
            if (degrees < 0) { degrees += 360;}
            float rad = degrees * DEG_TO_RAD;
            float cos = math.cos(rad), sin = math.sin(rad);
            return new Tile(
                (int)math.round(cos * data.x - sin * data.z), 
                data.y, 
                (int)math.round(sin * data.x + cos * data.z));
        }

        /// <summary>
        /// Convenience method that calculates if this tile is at the grid's edge.
        /// </summary>
        /// <returns>True if tile + dir would be outside the grid</returns>
        public readonly bool IsAtLimit(Tile dir, byte3 size)
        {
            return (dir.Equals(Down) && data.y == 0)
                    || (dir.Equals(Up) && data.y == size.y - 1)
                    || (dir.Equals(N) && data.z == size.z - 1)
                    || (dir.Equals(E) && data.x == size.x - 1)
                    || (dir.Equals(S) && data.z == 0)
                    || (dir.Equals(W) && data.x == 0);
        }

        /// <summary>
        /// Checks for walls in this tile in provided direction. In diagonal directions, both adjacent direct walls must be clear.
        /// Does not account for diagonal _movement_ as that is dependent on neighboring tiles.
        /// </summary>
        /// <param name="direction">The direction the check is performed towards.</param>
        /// <returns>True if direction is clear, false if blocked.</returns>
        public bool IsMovableTo(Tile direction)
        {
            WallMask dir = DirectionToWallMask(direction.Normalized);
            return (walls.GetMask(WallTypeMask.AllBlocked) & dir) == 0;
        }

        /// <summary>
        /// Checks for walls in this tile in provided direction. In diagonal directions, one of adjacent direct walls must be clear.
        /// </summary>
        /// <param name="direction">The direction the check is performed towards.</param>
        /// <returns>True if direction is visible, false if blocked.</returns>
        public bool IsVisibleTo(Tile direction)
        {
            WallMask dir = DirectionToWallMask(direction.Normalized);
            return (int)(walls.GetMask(WallTypeMask.Full) & dir) <= 1;
        }

        /// <summary>
        /// Converts a Wall enum to a direction Tile.
        /// </summary>
        /// <param name="wall"></param>
        /// <returns>The normalized Tile struct.</returns>
        public static Tile WallToDirection(Wall wall)
        {
            for (int i = 0; i < Directions_Cubic.Length; i++)
            {
                if ((int)(wall & (Wall)(1 << i)) == 1) { return Directions_Cubic[i]; }
            }

            return Zero;
        }

        /// <summary>
        /// Converts a Tile to a WallMask by normalizing it to a direction. Diagonal directions are converted to both their
        /// single components.
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static WallMask DirectionToWallMask(Tile direction)
        {
            Tile t = direction.Normalized;

            if (t.Equals(N)) { return WallMask.North; } // A little stupid wall of ifs but can't be arsed to open it
            if (t.Equals(E)) { return WallMask.East; }
            if (t.Equals(S)) { return WallMask.South; }
            if (t.Equals(W)) { return WallMask.West; }
            if (t.Equals(Up)) { return WallMask.Up; }
            if (t.Equals(Down)) { return WallMask.Down; }
            if (t.Equals(NE)) { return WallMask.NorthEast; }
            if (t.Equals(SE)) { return WallMask.SouthEast; }
            if (t.Equals(SW)) { return WallMask.SouthWest; }
            if (t.Equals(NW)) { return WallMask.NorthWest; }
            if (t.Equals(NUp)) { return WallMask.North | WallMask.Up; }
            if (t.Equals(NDown)) { return WallMask.North | WallMask.Down; }
            if (t.Equals(EUp)) { return WallMask.East | WallMask.Up; }
            if (t.Equals(EDown)) { return WallMask.East | WallMask.Down; }
            if (t.Equals(SUp)) { return WallMask.South | WallMask.Up; }
            if (t.Equals(SDown)) { return WallMask.South | WallMask.Down; }
            if (t.Equals(WUp)) { return WallMask.West | WallMask.Up; }
            if (t.Equals(WDown)) { return WallMask.West | WallMask.Down; }
            return WallMask.None;
        }

        public static (Tile left, Tile right) AdjacentDirections(Tile dir)
        {
            dir = dir.Normalized;
            if (dir.Equals(N)) { return (NW, NE); }
            if (dir.Equals(NE)) { return (N, E); }
            if (dir.Equals(E)) { return (NE, SE); }
            if (dir.Equals(SE)) { return (E, S); }
            if (dir.Equals(S)) { return (SE, SW); }
            if (dir.Equals(SW)) { return (S, W); }
            if (dir.Equals(W)) { return (SW, NW); }
            if (dir.Equals(NW)) { return (W, N); }
            return (MaxValue, MaxValue);
        }
        #endregion

        #region Static directions
        // All and Cubic must be in the same order so casting from Edge to Cover in a loop works correctly
        public static Tile[] Directions_All => new Tile[] { N, E, S, W, Up, Down, NE, SE, SW, NW, };
        public static Tile[] Directions_Cubic => new Tile[] { N, E, S, W, Up, Down };   
        public static Tile[] Directions_Direct => new Tile[] { N, E, S, W };        
        public static Tile[] Directions_Diagonal => new Tile[] { NE, SE, SW, NW };
        public static Tile[] Directions_Lateral => new Tile[] { N, E, S, W, NE, SE, SW, NW };
        public static Tile Zero => new(0, 0, 0);
        public static Tile One => new(1, 1, 1);
        public static Tile N => new(0, 0, 1);
        public static Tile NE => new(1, 0, 1);
        public static Tile E => new(1, 0, 0);
        public static Tile SE => new(1, 0, -1);
        public static Tile S => new(0, 0, -1);
        public static Tile SW => new(-1, 0, -1);
        public static Tile W => new(-1, 0, 0);
        public static Tile NW => new(-1, 0, 1);
        public static Tile Up => new(0, 1, 0);
        public static Tile Down => new(0, -1, 0);
        public static Tile NUp => new(0, 1, 1);
        public static Tile NDown => new(0, -1, 1);
        public static Tile EUp => new (1, 1, 0);
        public static Tile EDown => new(1, -1, 0);
        public static Tile SUp => new(0, 1, -1);
        public static Tile SDown => new(0, -1, -1);
        public static Tile WUp => new(-1, 1, 0);
        public static Tile WDown => new(-1, -1, 0);
        public static Tile MaxValue => new(sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue);
        #endregion

        #region Overrides and Interfaces
        /// <summary> NOTE: Copies node data from Node a. </summary>
        public static Tile operator +(Tile a, Tile b)
        {
            a.data = new sbyte3(a.data.x + b.data.x, a.data.y + b.data.y, a.data.z + b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node data from Node b </summary>
        public static Tile operator -(Tile a, Tile b)
        {
            b.data = new sbyte3(a.data.x - b.data.x, a.data.y - b.data.y, a.data.z - b.data.z);
            return b;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator *(Tile a, int k)
        {
            a.data = new sbyte3(a.data.x * k, a.data.y * k, a.data.z * k);
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator *(Tile a, float k)
        {
            a.data = new sbyte3((int)math.round(a.data.x * k), (int)math.round(a.data.y * k), (int)math.round(a.data.z * k));
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator +(Tile a, Wall w)
        {
            return a + WallToDirection(w);
        }

        public static implicit operator Vector3(Tile t)
        {
            return new Vector3(t.data.x, t.data.y, t.data.z);
        }

        public override int GetHashCode() 
        {
            return data.GetHashCode();
        }

        public override readonly string ToString()
        {
            return $"({data.x}, {data.y}, {data.z})";
        }

        public readonly bool Equals(Tile other)
        {
            return data.x == other.data.x && data.y == other.data.y && data.z == other.data.z;
        }
        #endregion
    }
}
