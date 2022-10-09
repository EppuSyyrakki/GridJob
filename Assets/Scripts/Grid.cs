using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace GridJob
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
						tiles[i] = new Tile(x, y, z, Edge.None, Cover.None, TileType.Empty, i);
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

		#region Graph building
		public void AutoBuild(bool log = false)
		{			
			LayerMask layers = Data.terrainLayer | Data.climbLayer;

			for (int i = 0; i < tiles.Length; i++)
			{
				Tile tile = tiles[i];				
				TileType type = BoxcastTileType(tile, layers, includeTriggers: true);
				tile.RemoveEdges(Edge.All);
				tile.SetType(type);
				tile.AddCovers(LinecastCover(tile));
				tiles[i] = tile;
			}

			// Tiles have no edges as default. Add them according to type and neighbors.
			GetTilesOfTypes(TileType.Empty, TileType.Climb, out var empties, out var climbs);

			foreach (Tile tile in empties)
            {
				Tile altered = HandleEmptys(tile);
				tiles[altered.index] = altered;
            }

			foreach (Tile tile in climbs)
            {
				Tile altered = HandleClimb(tile);
				tiles[altered.index] = altered;
            }
        }

        private Tile HandleEmptys(Tile tile)
		{
			if (tile.data.y == 0)	// Bottom floor, no need to check for neighbor below this
            {
				tile = AddLateralEdgesTowardNeighbors(tile);
				return tile;
			}

			if (!GetIndex(tile + Tile.down, Data, out int belowIndex)) { return tile; } 
				
			Tile below = tiles[belowIndex]; // A tile exists below this one

			if (below.IsAnyType(TileType.WalkableTypes))	// Tile below is free
			{
				tile.AddEdges(Edge.Down);
				tile.AddType(TileType.Jump);
                return tile;
			}

			tile = AddLateralEdgesTowardNeighbors(tile);    // Tile below is blocked
            return tile;
		}

		private Tile AddLateralEdgesTowardNeighbors(Tile tile)
        {
			var directions = Tile.Directions_Lateral;

			for (int i = 0; i < directions.Length; i++) // loop through all lateral neighbors
			{
                Edge e = Tile.DirectionToEdge(directions[i]);

				if (tile.HasAnyCover(e.ToCover())) { continue; }

				if (GetIndex(tile + directions[i], Data, out int neighborIndex))
				{
					Tile neighbor = tiles[neighborIndex];

					if (neighbor.IsAnyType(TileType.BlockedTypes)) { continue; }
					
					tile.AddEdges(e); // not blocked, add edge
				}					
			}

			directions = Tile.Directions_Diagonal;

			for (int i = 0; i < directions.Length; i++) // loop diagonal neighbors
			{
				Edge e = Tile.DirectionToEdge(directions[i]);
				var adjacents = e.Adjacents(); 

				// if it has both adjacent edges, it wont hug a corner, let it be
				if (tile.HasPassageTo(adjacents.e1) && tile.HasPassageTo(adjacents.e2)) { continue; }

				tile.RemoveEdges(e);	// it lacks at least one adjacent direct edge, remove it.
			}
			return tile;
		}

		/// <summary> Enables Edge.up, finds a blocked direction and enables the above tile edge to that direction. </summary>
		private Tile HandleClimb(Tile tile)
		{
			Tile above, below = new Tile();

			if (GetIndex(tile + Tile.up, Data, out int aboveIndex)) // Is there a tile above this?
			{
				above = tiles[aboveIndex];

                tile.AddEdges(Edge.Up);               				

				if (above.IsAnyType(TileType.Jump)) 
				{
					var directions = Tile.Directions_Direct;    // Only scan (N E S W) exiting climb tiles 

					for (int i = 0; i < directions.Length; i++)
					{
						// Loop neighbors, find any blocked and add the direction to that as edge to above tile
						if (!GetIndex(tile + directions[i], Data, out int neighborIndex)) { continue; }

						Tile neighbor = tiles[neighborIndex];  // Neighbor exists.

						if (!neighbor.IsAnyType(TileType.BlockedTypes)) { continue; }	// It's free
						
						Edge edge = Tile.DirectionToEdge(directions[i]);
						above.AddEdges(edge);
						tiles[above.index] = above;	
					}
				}
			}

			if (GetIndex(tile + Tile.down, Data, out int belowIndex)) 
			{
				below = tiles[belowIndex];	// A tile below exists
				
				if (below.IsAnyType(TileType.BlockedTypes))
				{ 					
					tile = AddLateralEdgesTowardNeighbors(tile);	// climb tiles above blockeds can be exited in any dir
				}
				else if (below.IsAnyType(TileType.WalkableTypes))
                {
					tile.AddEdges(Edge.Down);	// climb tiles over emptys can only be travelled down
                }
			}
            else
            {
				tile = AddLateralEdgesTowardNeighbors(tile);
			}

			return tile;
		}

		/// <summary>
		/// Linecasts from tile to all direct cubic directions on Data.coverLayer. Should only be called after
		/// TileTypes have been assigned.
		/// </summary>
		/// <param name="tile">The source tile</param>
		/// <returns>The Edges that have a cover object between the tiles</returns>
		private Cover LinecastCover(Tile tile)
		{
			var covers = Cover.None;

            foreach (var dir in Tile.Directions_Cover)
			{
				if (dir.Equals(Tile.down) && tile.data.y == 0
					|| dir.Equals(Tile.up) && tile.data.y == Data.size.y - 1
					|| dir.Equals(Tile.n) && tile.data.z == Data.size.z - 1
					|| dir.Equals(Tile.e) && tile.data.x == Data.size.x - 1
					|| dir.Equals(Tile.s) && tile.data.z == 0
					|| dir.Equals(Tile.w)&& tile.data.x == 0) { continue; } 

				covers |= LinecastCoverSingle(tile, dir);
            }          

			return covers;
		}

		private Cover LinecastCoverSingle(Tile tile, Tile dir)
		{
            if (GetIndex(tile + dir, Data, out int index) && tiles[index].IsAnyType(TileType.WalkableTypes)) 
			{
                var offset = Vector3.up * Data.cellSize.y * 0.5f;   // Vector3 from the tile "origin" to the middle.
                var from = TileToWorld(tile, Data) + offset;
                var to = TileToWorld(tile + dir, Data) + offset;

                if (Physics.Linecast(from, to, Data.coverLayer))
                {
                    return Tile.DirectionToEdge(dir).ToCover();
                }
            }

            return Cover.None;
        }

		/// <summary>
		/// Uses Physics.CheckBox to detect anything on given layers in the node's world position. 
		/// NOTE: CAN'T BE USED INSIDE JOBS B/C REGULAR PHYSICS CASTS FAIL THERE!
		/// </summary>
		private TileType BoxcastTileType(Tile tile, LayerMask layers, bool includeTriggers = false)
		{
			var pos = TileToWorld(tile, Data) + Vector3.up * Data.cellSize.y * 0.5f;
			var radius = Data.cellSize * Data.obstacleCastRadius;
			var interaction = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
			var colliders = Physics.OverlapBox(pos, radius, Quaternion.identity, layers, interaction);

			if (colliders.Length > 1)
			{
				Debug.LogWarning($"BoxcastTileType found {colliders.Length} colliders in {tile}." 
					+ " Check for multiple objects, or try reducing Obstacle Cast Radius");
				Debug.DrawLine(TileToWorld(tile, Data), TileToWorld(tile + Tile.up * Data.size.y, Data), Color.red, 10f);
			}

			if (colliders.Length > 0)
			{
				return GridData.LayerMapping(colliders[0].gameObject.layer, Data); 
			}

			return TileType.Empty;
		}

        #endregion

        #region Public API

        public static Vector3 TileToWorld(Tile tile, GridData data)
		{
			var world = data.transformPosition + LocalPosition(tile, data) 
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

		#endregion

		#region TileCasts

		public List<Tile> LineCast1(Tile a, Tile b)
		{
			var points = new List<Tile>((int)(b - a).Magnitude() + 1);
			int aX = a.data.x, aY = a.data.y, aZ = a.data.z; 
			int bX = b.data.x, bY = b.data.y, bZ = b.data.z;

			bool steepXY = math.abs(bY - aY) > math.abs(bX - aX);			
			if (steepXY) { Swap(ref aX, ref aY); Swap(ref bX, ref bY); }

			bool steepXZ = math.abs(bZ - aZ) > math.abs(aX - bX);
			if (steepXZ) { Swap(ref aX, ref aZ); Swap(ref bX, ref bZ); }

			int3 d = new int3(math.abs(bX - aX), math.abs(bY - aY), math.abs(bZ - aZ));
			int errorXY = d.x / 2, errorXZ = d.x / 2;

			int stepX = aX > bX ? -1 : 1;
            int stepY = aY > bY ? -1 : 1;
            int stepZ = aZ > bZ ? -1 : 1;

			int y = aY, z = aZ;
			Tile current;

			for (int x = aX; x != bX; x += stepX)
			{
				int cX = x, cY = y, cZ = z;

				if (steepXZ) { Swap(ref cX, ref cZ); }
				if (steepXY) { Swap(ref cX, ref cY); }

				current = new Tile(x, y, z);
				
				if (GetIndex(current, Data, out int index))
				{
					points.Add(tiles[index]);
				}

				if (errorXY < 0)
				{
					y += stepY;
					errorXY += d.x;						
				}

				if (errorXZ < 0)
				{
					z += stepZ;
					errorXZ += d.x;
				}
			}

			return points;

            static void Swap<T>(ref T a, ref T b)
            {
                T tmp = b;
                b = a;
                a = tmp;
            }
        }

		public List<Tile> LineCast2(Tile from, Tile to)
		{			
			Tile v = (to - from);
            var points = new List<Tile>((int)v.Magnitude());
            // v.data = v.data.Abs;
            float x = from.data.x;
			float y = from.data.y;
			float z = from.data.z;
			var size = Data.size;
			float stepX = v.data.x < 0 ? -1 : 1;
			float stepY = v.data.y < 0 ? -1 : 1;
			float stepZ = v.data.z < 0 ? -1 : 1;
			Tile current = from;

			while (true) // The heart of the line draw algorithm
			{
				if (v.data.x < v.data.y)
				{
					if (v.data.x < v.data.z)
					{
						x += stepX;
						if (x > size.x) { break; }  // outside grid
					}
					else
					{
						z += stepZ;
						if (z > size.z) { break; }
					}
				}
				else
				{
					if (v.data.y < v.data.z)
					{
						y += stepY;
						if (y > size.y) { break; }
					}
					else
					{
						z += stepZ;
						if (z > size.z) { break; }
					}
				}

				Debug.DrawLine(TileToWorld(current, Data), TileToWorld(new Tile(x, y, z), Data), Color.red, 5f);
				current = new Tile(x, y, z);
				Debug.Log(current);
				// points.Add(tiles[GetIndex(current, Data)]);
			}

			return points;
		}

		public List<Tile> LineCast3(Tile a, Tile b)
		{
			float dx = b.data.x - a.data.x;
            float dy = b.data.y - a.data.y;
            float dz = b.data.z - a.data.z;
			float deltaErrorY = math.abs(dy / dx);
			float deltaErrorZ = math.abs(dz / dx);
			float errorY = 0;
			float errorZ = 0;
			float y = a.data.y;
			float z = a.data.z;
			var result = new List<Tile>((int)(b - a).Magnitude());
			Tile current;

			for (int x = a.data.x; x < b.data.x; x++)
			{
				current = new Tile(x, y, z);

				if (GetIndex(current, Data, out int index))
				{
					result.Add(tiles[index]);
				}

				errorY += deltaErrorY;
				while(errorY >= 0.5f)
				{
					y += math.sign(dy);
					errorY--;
				}

				errorZ += deltaErrorZ;
				while(errorZ >= 0.5f)
				{
					z += math.sign(dz);
					errorZ--;
				}
			}

			return result;
        }

		#endregion

		#region Tile Get

		/// <summary> Finds and returns a list of all tiles of types flagged in param types. </summary>
		public List<Tile> GetTilesOfType(TileType types)
		{
			List<Tile> list = new List<Tile>(tiles.Length);

			for (int i = 0; i < tiles.Length; i++)
			{
				if (tiles[i].IsAnyType(types)) { list.Add(tiles[i]); }
			}
			return list;
		}

		/// <summary> Finds and returns a list of all tiles of given types. </summary>
		public void GetTilesOfTypes(TileType t1, TileType t2, out List<Tile> l1, out List<Tile> l2)
		{
			l1 = new List<Tile>();
			l2 = new List<Tile>();

			for (int i = 0; i < tiles.Length; i++)
			{
				Tile tile = tiles[i];

				if (tile.IsAnyType(t1)) { l1.Add(tile); }
				else if (tile.IsAnyType(t2)) { l2.Add(tile); }
			}
		}

		/// <summary> Loops tiles and adds them to lists according to their type. </summary>
		public void GetTilesOfTypes(TileType t1, TileType t2, TileType t3, out List<Tile> l1, out List<Tile> l2, out List<Tile> l3)
        {
			l1 = new List<Tile>();
			l2 = new List<Tile>();
			l3 = new List<Tile>();

			for (int i = 0; i < tiles.Length; i++)
			{
				Tile tile = tiles[i];
				
				if (tile.IsAnyType(t1)) { l1.Add(tile); }
				else if (tile.IsAnyType(t2)) { l2.Add(tile); }
				else if (tile.IsAnyType(t3)) { l3.Add(tile); }
			}
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
			if (dir.IsAnyType(TileType.Jump)) { return 0; }
			var d = dir.Normalized().data;
			if (d.y > 0) { return data.upCost; }
			if (math.abs(d.x) + math.abs(d.z) == 1) { return data.directCost; }
			if (math.abs(d.x) + math.abs(d.z) == 2) { return data.diagonalCost; }

			return data.directCost;
		}	

		/// <summary> Convenience method to immediately know if index != -1. </summary>
		public static bool GetIndex(Tile t, GridData data, out int index)
        {
			index = GetIndex(t, data);
			if (index == -1) { return false; }
			return true;
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

        private static Tile Lerp(in Tile a, in Tile b, in float t)
        {
            return new Tile(
                math.round(Lerp(in a.data.x, in b.data.x, in t)),
                math.round(Lerp(in a.data.y, in b.data.y, in t)),
                math.round(Lerp(in a.data.z, in b.data.z, in t))
                );
        }

        private static float Lerp(in sbyte a, in sbyte b, in float t)
        {
            return a + t * (b - a);
        }

		private static float Min(float3 f)
		{
			if (f.x < f.y && f.x < f.z) return f.x;
			if (f.y < f.x && f.y < f.z) return f.y;
			return f.z;
		}

        private static float Mid(float3 f)
        {
			if (f.x < f.y && f.x > f.z) return f.x;
			if (f.y < f.x && f.y > f.z) return f.y;
			return f.z;
        }

        private static float Max(float3 f)
        {
			if (f.x > f.y && f.x > f.z) return f.x;
			if (f.y > f.x && f.y > f.z) return f.y;
			return f.z;
        }

        #endregion
    }
}
