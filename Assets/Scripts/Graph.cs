using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Jobben 
{
	public struct Graph
	{
		public static int TILES_MAX = 65536;

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
			Assert.IsTrue((data.size.x * data.size.y * data.size.z) <= TILES_MAX);
			Data = data;
			tiles = new Tile[data.size.x * data.size.y * data.size.z];
			
			int i = 0;

			for (int z = 0; z < data.size.z; z++)
			{
				for (int y = 0; y < data.size.y; y++)
				{
					for (int x = 0; x < data.size.x; x++)
					{
						tiles[i] = EmptyEdges(new Tile(x, y, z, Edge.All, TileType.Empty, false, i), data);
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
			Assert.IsTrue((asset.Data.size.x * asset.Data.size.y * asset.Data.size.z) <= TILES_MAX);
			tiles = new Tile[asset.Tiles.Length];
			Data = asset.LoadFromAsset(out tiles);
			initialized = true;

			if (log)
			{
				int3 s = asset.Data.size;
				Debug.Log(this + $" loaded {Tiles.Length} tiles from {s.x} * {s.y} * {s.z} in asset {asset.name}");
			}	
		}
		#endregion

		#region Graph building

		/*	Auto build notes:
		 *	1. Loop all nodes 
		 *		1.1. Raycast each for Terrain layer. Set Edges.None and TileType.Terrain to blocked tiles.
		 *		1.2. Add current to blocked list.
		 *		1.3. Disable Edge.Down from above (if exists)
		 *	2. Iterate blocked list
		 *	3. If node.data.y < data.size.y - 1 (below highest level), do: 
		 *		3.1. Get current + Node.up if it's not in the blocked nodes list
		 *		3.2. Scan it for lateral neighbors that are not in the blocked nodes
		 *		3.3. Enable edge toward existing neighbor
		 *		3.4. If the neighbor doesnt have Edge.Down, enable the opposite edge for the neighbor.
		 *		3.5. goto 3
		 *	4. Assign from blocked list to nodes[]
		*/

		public void AutoBuild()
        {
			var blockedList = new List<Tile>(tiles.Length / 2);

			for (int i = 0; i < tiles.Length; i++)	// 1.
			{
				Tile t = tiles[i];
				//t = EmptyEdges(t, Data);	// Perhaps not needed here as the Constructor already does this

				if (BoxcastTile(t, Data, Data.terrain))	// 1.1
				{ 
					t.SetEdges(Edge.None);
					t.SetType(TileType.Terrain);
					blockedList.Add(t);	// 1.2

					if (HasTile(t + Tile.up, Data)) // 1.3
					{
						Tile above = tiles[CalculateIndex(t + Tile.up, Data.size)];
						above.RemoveEdges(Edge.Down);	
						tiles[above.index] = above;
                    }
				}

				tiles[i] = t;
			}

            for (int i = 0; i < tiles.Length; i++)
            {
                Tile t = DisconnectMissingEdges(tiles[i], Data, in tiles);
                tiles[i] = t;
            }

            Tile[] laterals = Tile.Directions_Lateral;

			foreach (Tile t in blockedList)	// 2.
            {
				if (t.data.y < Data.size.y - 1)	// 3.
                {
					if (blockedList.Contains(t + Tile.up)) { continue; }

					Tile above = tiles[CalculateIndex(t + Tile.up, Data.size)];  // 3.1					
                    
					for (int i = 0; i < laterals.Length; i++)
                    {
						if (HasTile(above + laterals[i], Data) && !blockedList.Contains(above + laterals[i]))	// 3.2
						{
							Tile aboveNeighbor = tiles[CalculateIndex(above + laterals[i], Data.size)];
							above.AddEdges(Tile.DirectionToEdge(laterals[i]));	// 3.3

							if (aboveNeighbor.HasEdge(Edge.Down)) { continue; }

							aboveNeighbor.AddEdges(Tile.DirectionToEdge(laterals[i] * -1));   // 3.4
							tiles[aboveNeighbor.index] = aboveNeighbor;
                        }
                    }

					tiles[above.index] = above;
                }
            }
		}

		/// <summary>
		/// Disables edges at size limits, all up edges, and all lateral edges when y > 0.
		/// </summary>
		private static Tile EmptyEdges(Tile t, MapData data)
        {
			int3 i = t.data;
			t.RemoveEdges(Edge.Up);
			
			if (i.x == 0) { t.RemoveEdges(Edge.SouthWest | Edge.West | Edge.NorthWest); }			
			else if (i.x == data.size.x - 1) { t.RemoveEdges(Edge.NorthEast | Edge.East | Edge.SouthEast); }
			
			if (i.y == 0) { t.RemoveEdges(Edge.Down); }
			else if (i.y > 0) { t.RemoveEdges(Edge.AllSameLevel); }

			if (i.z == 0) { t.RemoveEdges(Edge.SouthEast | Edge.South | Edge.SouthWest); }
			if (i.z == data.size.z - 1) { t.RemoveEdges(Edge.NorthWest | Edge.North | Edge.NorthEast); }

			return t;
        }
		 
		/// <summary>
		/// Check each of node's possible neighbors to check if they have a common edge (the opposite edge for the neighbor). 
		/// Down direction is passed over if is not blocked. If not, the edge is removed from the node before returning.
		/// TODO: The nodes array is read only, but SHOULD NOT BE USED IN JOBS ATM
		/// </summary>
		public static Tile DisconnectMissingEdges(Tile t, MapData data, in Tile[] tiles)
        {
			Tile[] directions = Tile.Directions_All;
			Edge edgesToRemove = 0;

			for (int i = 0; i < directions.Length; i++)
            {
				Tile candidate = t + directions[i];
				int index = CalculateIndex(candidate, data.size);

				if (!t.HasEdgeTo(candidate) || index < 0 || index > data.Length
					|| (directions[i].Equals(Tile.down) && tiles[index].Type != TileType.Terrain))  
				{ 
					continue; 
				}

				Tile neighbor = tiles[index];			

				if (!neighbor.HasEdgeTo(t)) { edgesToRemove |= Tile.DirectionToEdge(directions[i]); }
            }

			t.RemoveEdges(edgesToRemove);
			return t;          
        }

		/// <summary>
		/// Uses Physics.CheckBox to detect anything on given layers in the node's world position. 
		/// NOTE: SHOULD NOT BE USED INSIDE JOBS.
		/// </summary>
		private static bool BoxcastTile(Tile t, MapData data, LayerMask layers, bool includeTriggers = false, bool debug = false)
		{
			var pos = TileToWorld(t, data) + Vector3.up * data.cellSize.y * 0.5f;
			var halfExtents = data.cellSize * data.obstacleCastRadius;
			var interaction = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

			if (Physics.CheckBox(pos, halfExtents, Quaternion.identity, layers, interaction))
			{
				if (debug) { Debug.DrawLine(pos, pos + Vector3.up, Color.red, 10f);  }

				return true;
			}

			if (debug) { Debug.DrawLine(pos, pos + Vector3.up, Color.green, 10f); }
			return false;
		}

		#endregion

		//public static List<Node> GetNeighbors(Node node, Node[] grid, MapData data)
		//{
		//    var directions = Node.Directions_All;
		//    var neighbors = new List<Node>(directions.Length);

		//    for (int i = 0; i < directions.Length; i++)
		//    {
		//        Edge current = (Edge)(1 << i);
		//        Node neighbor = node + directions[i];

		//        if (HasNode(neighbor, data.size) && node.HasEdge(current) && !node.occupied)
		//        {
		//            int neighborIndex = CalculateIndex(neighbor, data.size);
		//            neighbors.Add(grid[neighborIndex]);
		//        }
		//    }

		//    return neighbors;
		//}

		public static Vector3 TileToWorld(Tile t, MapData data)
        {
			var world = data.transformPosition + LocalPosition(t, data) + new Vector3(data.cellSize.x * 0.5f, 0f, data.cellSize.z * 0.5f);
			var worldOffset = new Vector3(world.x, world.y, world.z);
			return worldOffset;
        }

		public Tile WorldToNode(Vector3 worldPos)
        {
			Vector3 local = worldPos - Data.transformPosition;
			Vector3 scaled = new Vector3(local.x / Data.cellSize.x, local.y / Data.cellSize.y, local.z / Data.cellSize.z);
			int3 location = new int3((int)scaled.x, (int)math.round(scaled.y), (int)scaled.z);
			return HasTile(location, Data.size) ? tiles[CalculateIndex(location, Data.size)] : Tile.MaxValue;
		}

		public void Set(Tile[] tiles, MapData data)
		{
			Assert.AreEqual(data.size.x * data.size.y * data.size.z, tiles.Length);
			Data = data;
			this.tiles = new Tile[tiles.Length];

			for (int i = 0; i < tiles.Length; i++) { this.tiles[i] = tiles[i]; }
		}

		#region Statics
		/// <summary>
		/// Node's location relative to the object owning this graph.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static Vector3 LocalPosition(Tile t, MapData data)
		{
			return new Vector3(t.data.x * data.cellSize.x, t.data.y * data.cellSize.y, t.data.z * data.cellSize.z);
		}

		/// <summary>
		/// Simple conversion from a direction to a cost.
		/// </summary>
		public static int Cost(Tile dir, MapData data)
        {
			var d = dir.Normalized().data;
			if (d.y > 0) { return data.upCost; }
			if (math.abs(d.x) + math.abs(d.z) == 1) { return data.directCost; }
            if (math.abs(d.x) + math.abs(d.z) == 2) { return data.diagonalCost; }

            return data.diagonalCost;
        }

        public static int ManhattanDistance(Tile a, Tile b, MapData data)
		{
            int3 dist = a.data - b.data;
            int height = dist.y > 0 ? data.upCost : data.directCost;
            return math.abs(data.diagonalCost * math.min(dist.x, dist.z)) 
				+ math.abs(data.directCost * math.max(dist.x, dist.z))
				+ math.abs(dist.y * height);
        }

        public static int CalculateIndex(Tile t, int3 size)
        {
			return CalculateIndex(t.data.x, t.data.y, t.data.z, size);
		}

        public static int CalculateIndex(int3 i, int3 size)
        {
			return CalculateIndex(i.x, i.y, i.z, size);
		}

		public static int CalculateIndex(int x, int y, int z, int3 size)
		{
			// return (z << (size.x + size.y)) + (y << size.x) + x;
			return (z * size.x * size.y) + (y * size.x) + x; // Unoptimized version, can be used with non-n^2 grids.
		}

		public static bool HasTile(Tile t, MapData data)
        {
			return HasTile(t.data, data.size);
        }

		public static bool HasTile(Tile t, int3 size)
        {
			return HasTile(t.data, size);
        }

		public static bool HasTile(int3 i, int3 size)
        {
			return i.x < size.x && i.y < size.y && i.z < size.z && i.x > -1 && i.y > -1 && i.z > -1;
		}

		private static bool IsPowerOfTwo(int3 i)
		{
			if (i.x == 0 || i.y == 0 || i.z == 0) { return false; }
            var result = (int3)math.ceil(math.log(i) / math.log(2)) == (int3)math.floor(math.log(i) / math.log(2));
			return result[0] && result[1] && result[2];
		}
		#endregion
	}
}
