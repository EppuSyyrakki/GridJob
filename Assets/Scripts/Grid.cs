using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;
using Unity.Collections;

namespace GridSystem
{    
    public struct Grid
	{
		public static readonly float SQRT2 = 1.41421356f;
		public static readonly float SQRT3 = 1.7320508f;

        public GridData Data { get; private set; }
		public Tile[] Tiles { get { return tiles; } }

		private Tile[] tiles;

		private bool initialized;

		public bool IsInitialized => initialized;

		#region Constructors
		/// <summary>
		/// Creates a fresh grid. Total size must not exceed 65536 (128 * 128 * 4)
		/// </summary>
		public Grid(GridData data, bool log = false)
		{
			Assert.IsTrue(data.EnsureSize());
			Data = data;
			tiles = new Tile[data.size.x * data.size.y * data.size.z];

			int i = 0;

			for (int z = 0; z < data.size.z; z++)
			{
				for (int y = 0; y < data.size.y; y++)
				{
					for (int x = 0; x < data.size.x; x++)
					{
						tiles[i] = new Tile(x, y, z, new Walls(), i);
						i++;
					}
				}
			}

			initialized = true;

			if (log)
			{
				Debug.Log(this + $" created {Tiles.Length} tiles from {data.size.x} * {data.size.y} * {data.size.z} from MapData");
			}
		}

        /// <summary>
        /// Creates a Grid object from a previously saved GridAsset. Total size must not exceed 65536 (128 * 128 * 4)
        /// </summary>
        public Grid(GridAsset asset, bool log = false)
		{
			Assert.IsTrue(asset.Data.EnsureSize());
			tiles = new Tile[asset.Tiles.Length];
			Data = asset.LoadFromAsset(out tiles);
			initialized = true;

			if (log)
			{
                byte3 s = asset.Data.size;
				Debug.Log(this + $" loaded {Tiles.Length} tiles from {s.x} * {s.y} * {s.z} in asset {asset.name}");
			}
		}
        #endregion

        #region Public API

        [BurstCompatible]
        public static Vector3 TileToWorld(Tile tile, GridData data)
		{
			Vector3 world = data.transformPosition + LocalPosition(tile, data) 
				+ new Vector3(data.cellSize.x * 0.5f, 0f, data.cellSize.z * 0.5f);
			var worldOffset = new Vector3(world.x, world.y, world.z);
			return worldOffset;
		}

		public Tile WorldToTile(Vector3 worldPos)
		{
			Vector3 local = worldPos - Data.transformPosition;
			Vector3 scaled = new Vector3(local.x / Data.cellSize.x, local.y / Data.cellSize.y, local.z / Data.cellSize.z);
			Tile tile = new Tile(new sbyte3((sbyte)scaled.x, (sbyte)math.round(scaled.y), (sbyte)scaled.z));
			return GetIndex(tile, Data, out int index) ? tiles[index] : Tile.MaxValue;
		}

		public void Set(Tile[] tiles, GridData data)
		{
			Assert.AreEqual(data.size.x * data.size.y * data.size.z, tiles.Length);
			Data = data;
			this.tiles = new Tile[tiles.Length];

			for (int i = 0; i < tiles.Length; i++) { this.tiles[i] = tiles[i]; }
		}

        public bool UpdateTile(Tile t)
        {
            try
            {
                tiles[t.index] = t;
                return true;
            }
            catch (IndexOutOfRangeException e)
            {
                Debug.LogError(e);
                return false;
            }
        }

		public bool SetTiles(Tile[] tiles)
		{
			if (GridData.EnsureSize(tiles.Length))
			{
				this.tiles = tiles;
				return true;
			}

			return false;
		}

		#endregion

		#region TileCasts

        public List<Tile> Linecast(Tile a, Tile b, bool includeA = false)
		{
            Point axz = new(a.data.x, a.data.z);
            Point bxz = new(b.data.x, b.data.z);
            Point axy = new(a.data.x, a.data.y);
            Point bxy = new(b.data.x, b.data.y);
            int dist = math.max(Point.DiagonalDistance(axz, bxz), Point.DiagonalDistance(axy, bxy));
            List<Point> xz = Line(axz, bxz, dist);
			List<Point> xy = Line(axy, bxy, dist);            
			List<Tile> result = new List<Tile>(dist);

			for (int i = includeA ? 0 : 1; i < xz.Count; i++)
			{
				sbyte x = xz[i].q;
				sbyte y = xy[i].r; 
				sbyte z = xz[i].r;
				
				if (!GetIndex(new Tile(x, y, z), Data, out int index)) { break; }

				Tile tile = tiles[index];
				result.Add(tile); 
			}

            return result;
		}

		private List<Point> Line(Point p0, Point p1, int dist)
		{            
            List<Point> points = new List<Point>(dist);			

			for (int step = 0; step <= dist; step++)
			{
				float t = dist == 0 ? 0f : (float)step / dist;
				points.Add(Point.Lerp(p0, p1, t));
			}

			return points;
		}

        #endregion

        #region Statics

        /// <summary> Node's location relative to the object owning this graph. </summary>
        public static Vector3 LocalPosition(Tile t, GridData data)
		{
			return new Vector3(t.data.x * data.cellSize.x, t.data.y * data.cellSize.y, t.data.z * data.cellSize.z);
		}

		/// <summary> Simple conversion from a direction to a cost. </summary>		
		public static int Cost(Tile dir, GridData data)
		{
            // TODO: Handle jumping, climbing and dropping better, and possible unit-specific costs
            if (dir.Equals(Tile.Down)) { return (int)(data.directCost * 0.5f); }
			var d = dir.data.Normalized;
			if (d.y > 0) { return data.upCost; }
			if (math.abs(d.x) + math.abs(d.z) == 1) { return data.directCost; }
			if (math.abs(d.x) + math.abs(d.z) == 2) { return data.diagonalCost; }

			return data.directCost;
		}	

		/// <summary> Convenience method to immediately know if index != -1. </summary>
		public static bool GetIndex(Tile t, GridData data, out int index)
        {
			index = GetIndex(t, data);
			return index != -1;
        }

		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>		
		public static int GetIndex(Tile t, GridData data){ return GetIndex(t.data.x, t.data.y, t.data.z, data.size); }

		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int GetIndex(Tile t, byte3 size) { return GetIndex(t.data.x, t.data.y, t.data.z, size); }

		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int GetIndex(sbyte3 d, byte3 size) {return GetIndex(d.x, d.y, d.z, size);}

		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int GetIndex(int x, int y, int z, byte3 size)
		{
			if (x >= size.x || y >= size.y || z >= size.z || x < 0 || y < 0 || z < 0) { return -1; }
			int value = (z * size.x * size.y) + (y * size.x) + x; 
			return value;
		}

		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int GetIndex(sbyte x, sbyte y, sbyte z, byte3 size)
		{
			if (x >= size.x || y >= size.y || z >= size.z || x < 0 || y < 0 || z < 0) { return -1; }
			int value = (z * size.x * size.y) + (y * size.x) + x;
			return value;
		}

		public static bool HasTile(Tile t, GridData data) { return HasTile(t.data, data.size); }
        public static bool HasTile(Tile t, byte3 size) { return HasTile(t.data, size); }
        public static bool HasTile(sbyte3 d, byte3 size) { return GetIndex(d, size) != -1; }
      
        #endregion
    }

    /// <summary>
    /// Helper struct when using the line drawing algorithm in Linecast.
    /// </summary>
    internal struct Point : IEquatable<Point>
    {
        public readonly sbyte q, r;
        public Point(sbyte q, sbyte r) { this.q = q; this.r = r; }
        public Point(float q, float r) { this.q = (sbyte)math.round(q); this.r = (sbyte)math.round(r); }

        public static Point Lerp(in Point a, in Point b, in float t)
        {
            return new Point(
                math.round(Lerp(in a.q, in b.q, in t)),
                math.round(Lerp(in a.r, in b.r, in t))
                );
        }

        public static int DiagonalDistance(Point from, Point to)
        {
            return math.max(math.abs(to.q - from.q), math.abs(to.r - from.r));
        }

        private static float Lerp(in sbyte a, in sbyte b, in float t)
        {
            return a + t * (b - a);
        }

		public override int GetHashCode()
		{
			return q + (r << 4);
        }

		public bool Equals(Point other)
		{
			return q == other.q && r == other.r;
		}
	}
}
