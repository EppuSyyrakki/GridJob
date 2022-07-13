using System.Collections;
using UnityEngine;

namespace Jobben
{
	[CreateAssetMenu(fileName = "Map Asset", menuName = "GridJob/New Map Asset", order = 0)]
	public class MapAsset : ScriptableObject
	{
		[SerializeField]
		bool debugOperations = false;

		[SerializeField]
		private Graph graph;

		[SerializeField]
		private MapData data;

        [SerializeField]
		private Tile[] tiles;

		public Tile[] Tiles => tiles;
		public MapData Data => data;

        [ContextMenu("Create fresh from data")]
		private void CreateFromMapData()
        {
			graph = new Graph(Data, debugOperations);
			tiles = graph.Tiles;
        }

		[ContextMenu("Erase tiles")]
		public void EraseAsset()
		{
			tiles = null;
		}

		public bool HasData
		{
			get
			{
				if (tiles == null) { return false; }
				return tiles.Length > 0;
			}
		}

		public void SaveToAsset(Tile[] tiles, MapData mapData)
		{
			this.tiles = new Tile[tiles.Length];
			for (int i = 0; i < tiles.Length; i++) { this.tiles[i] = tiles[i]; }
			data = mapData;
			Debug.Log($"Saved {tiles.Length} tiles in {mapData.size.x} * {mapData.size.y} * {mapData.size.z} map to asset: {this}");
		}

		public MapData LoadFromAsset(out Tile[] tiles)
		{
			if (this.tiles == null || this.tiles.Length == 0)
			{
				tiles = null;
				Debug.LogError("MapData " + name + " has no stored Map!");
				return new MapData();
			}

			tiles = new Tile[this.tiles.Length];
			for (int i = 0; i < this.tiles.Length; i++) { tiles[i] = this.tiles[i]; }			
			return Data;
		}

		public bool UpdateTile(Tile tile)
        {
			if (!Graph.CalculateIndex(tile, data, out int index)) { return false; }

			tiles[index] = tile;
			return true;
        }
	}
}