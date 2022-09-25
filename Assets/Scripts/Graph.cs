using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace GridJob
{
	public struct Graph
	{
		public MapData Data { get; private set; }
		public Tile[] Tiles { get { return tiles; } }

		private Tile[] tiles;

		private bool initialized;

		public bool IsInitialized => initialized;

		#region Constructors
		/// <summary>
		/// Creates a fresh grid. Total size must not exceed 65536 (128 * 128 * 4)
		/// </summary>
		public Graph(MapData data, bool log = false)
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
						tiles[i] = new Tile(x, y, z, Edge.None, TileType.Empty, i);
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

		public Graph(MapAsset asset, bool log = false)
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
			LayerMask layers = Data.terrainLayer | Data.climbLayer | Data.coverLayer;

			for (int i = 0; i < tiles.Length; i++)
			{
				Tile tile = tiles[i];				
				TileType type = BoxcastTileType(tile, Data, layers, includeTriggers: true);
				tile.RemoveEdges(Edge.All);
				tile.SetType(type);
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

			if (!CalculateIndex(tile + Tile.down, Data, out int belowIndex)) { return tile; } 
				
			Tile below = tiles[belowIndex]; // A tile exists below this one

			if (below.IsAnyType(TileType.WalkableTypes))	// Tile below is free
			{
				tile.AddEdges(Edge.Down);
				tile.AddType(TileType.Jump);
				return tile;
			}

			tile = AddLateralEdgesTowardNeighbors(tile);	// Tile below is blocked
			return tile;
		}

		private Tile AddLateralEdgesTowardNeighbors(Tile tile)
        {
			var directions = Tile.Directions_Lateral;

			for (int i = 0; i < directions.Length; i++) // loop through all lateral neighbors
			{  
				if (CalculateIndex(tile + directions[i], Data, out int neighborIndex))
				{
					Tile neighbor = tiles[neighborIndex];

					if (neighbor.IsAnyType(TileType.BlockedTypes)) { continue; }

					Edge e = Tile.DirectionToEdge(directions[i]);
					tile.AddEdges(e); // not blocked, add edge
				}					
			}

			directions = Tile.Directions_Diagonal;

			for (int i = 0; i < directions.Length; i++) // loop diagonal neighbors
			{
				Edge e = Tile.DirectionToEdge(directions[i]);
				var adjacents = e.Adjacents(); 

				// if it has both adjacent edges, it wont hug a corner, let it be
				if (tile.HasAnyEdge(adjacents.e1) && tile.HasAnyEdge(adjacents.e2)) { continue; }

				tile.RemoveEdges(e);	// it lacks at least one adjacent direct edge, remove it.
			}
			return tile;
		}

		/// <summary> Enables Edge.up, finds a blocked direction and enables the above tile edge to that direction. </summary>
		private Tile HandleClimb(Tile tile)
		{
			Tile above, below = new Tile();

			if (CalculateIndex(tile + Tile.up, Data, out int aboveIndex)) 
			{
				above = tiles[aboveIndex];
				tile.AddEdges(Edge.Up);				

				if (above.IsAnyType(TileType.Jump)) 
				{
					var directions = Tile.Directions_Direct;    // Only scan (N E S W) exiting climb tiles 

					for (int i = 0; i < directions.Length; i++)
					{
						// Loop neighbors, find any blocked and add the direction to that as edge to above tile
						if (!CalculateIndex(tile + directions[i], Data, out int neighborIndex)) { continue; }

						Tile neighbor = tiles[neighborIndex];  // Neighbor exists.

						if (!neighbor.IsAnyType(TileType.BlockedTypes)) { continue; }	// It's free
						
						Edge edge = Tile.DirectionToEdge(directions[i]);
						above.AddEdges(edge);
						tiles[above.index] = above;	
					}
				}
			}

			if (CalculateIndex(tile + Tile.down, Data, out int belowIndex)) 
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
		/// Uses Physics.CheckBox to detect anything on given layers in the node's world position. 
		/// NOTE: CAN'T BE USED INSIDE JOBS B/C REGULAR PHYSICS CASTS FAIL THERE!
		/// </summary>
		private TileType BoxcastTileType(Tile tile, MapData data, LayerMask layers, bool includeTriggers = false)
		{
			var pos = TileToWorld(tile, data) + Vector3.up * data.cellSize.y * 0.5f;
			var radius = data.cellSize * data.obstacleCastRadius;
			var interaction = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
			var colliders = Physics.OverlapBox(pos, radius, Quaternion.identity, layers, interaction);

			if (colliders.Length > 1)
			{
				Debug.LogWarning($"BoxcastTileType found {colliders.Length} colliders in {tile}");
				Debug.DrawLine(TileToWorld(tile, Data), TileToWorld(tile + Tile.up * Data.size.y, Data), Color.red, 10f);
			}

			if (colliders.Length > 0)
			{
				return MapData.LayerMapping(colliders[0].gameObject.layer, data); 
			}

			return TileType.Empty;
		}
		#endregion

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

		public static Vector3 TileToWorld(Tile tile, MapData data)
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
			return CalculateIndex(tile, Data, out int index) ? tiles[index] : Tile.MaxValue;
		}

		public void Set(Tile[] tiles, MapData data)
		{
			Assert.AreEqual(data.size.x * data.size.y * data.size.z, tiles.Length);
			Data = data;
			this.tiles = new Tile[tiles.Length];

			for (int i = 0; i < tiles.Length; i++) { this.tiles[i] = tiles[i]; }
		}

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
		public static Vector3 LocalPosition(Tile t, MapData data)
		{
			return new Vector3(t.data.x * data.cellSize.x, t.data.y * data.cellSize.y, t.data.z * data.cellSize.z);
		}
		/// <summary> Simple conversion from a direction to a cost. </summary>
		public static int Cost(Tile dir, MapData data)
		{
			if (dir.IsAnyType(TileType.Jump)) { return 0; }
			var d = dir.Normalized().data;
			if (d.y > 0) { return data.upCost; }
			if (math.abs(d.x) + math.abs(d.z) == 1) { return data.directCost; }
			if (math.abs(d.x) + math.abs(d.z) == 2) { return data.diagonalCost; }

			return data.directCost;
		}	
		/// <summary> Convenience method to immediately know if index != -1. </summary>
		public static bool CalculateIndex(Tile t, MapData data, out int index)
        {
			index = CalculateIndex(t, data);
			if (index == -1) { return false; }
			return true;
        }
		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int CalculateIndex(Tile t, MapData data)
		{
			return CalculateIndex(t.data.x, t.data.y, t.data.z, data.size);
		}
		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int CalculateIndex(Tile t, byte3 size)
		{
			return CalculateIndex(t.data.x, t.data.y, t.data.z, size);
		}
		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int CalculateIndex(sbyte3 d, byte3 size)
		{
			return CalculateIndex(d.x, d.y, d.z, size);
		}
		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int CalculateIndex(int x, int y, int z, byte3 size)
		{
			if (x >= size.x || y >= size.y || z >= size.z || x < 0 || y < 0 || z < 0) { return -1; }
			int value = (z * size.x * size.y) + (y * size.x) + x; 
			return value;
		}
		/// <summary> Calculates the index of a tile with the size. -1 if out of bounds.</summary>
		public static int CalculateIndex(sbyte x, sbyte y, sbyte z, byte3 size)
		{
			if (x >= size.x || y >= size.y || z >= size.z || x < 0 || y < 0 || z < 0) { return -1; }
			int value = (z * size.x * size.y) + (y * size.x) + x;
			return value;
		}

		public static bool HasTile(Tile t, MapData data)
        {
            return HasTile(t.data, data.size);
        }
        public static bool HasTile(Tile t, byte3 size)
        {
            return HasTile(t.data, size);
        }
        public static bool HasTile(sbyte3 d, byte3 size)
        {
            return CalculateIndex(d, size) != -1;
        }
        #endregion
    }
}
