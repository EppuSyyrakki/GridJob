using System.Collections;
using UnityEditor;
using UnityEngine;

namespace GridSystem
{
	[CreateAssetMenu(fileName = "Map Asset", menuName = "GridJob/New Map Asset", order = 0)]
	public class GridAsset : ScriptableObject
	{
		[SerializeField]
		private GridData data;

        [SerializeField]
		private Tile[] tiles;

		public Tile[] Tiles => tiles;
		public GridData Data => data;

		[ContextMenu("Erase data (Destructive!)")]
		public void EraseAsset()
		{
			tiles = null;
			data = default;
			EditorUtility.SetDirty(this);
		}

		public bool HasData
		{
			get
			{
				if (tiles == null) { return false; }
				return tiles.Length > 0;
			}
		}

		public void SaveToAsset(Tile[] tiles, GridData mapData)
		{
			this.tiles = new Tile[tiles.Length];
			for (int i = 0; i < tiles.Length; i++) { this.tiles[i] = tiles[i]; }
			data = mapData;
			EditorUtility.SetDirty(this);
			Debug.Log($"Saved {tiles.Length} tiles in {mapData.size.x} * {mapData.size.y} * {mapData.size.z} map to asset: {this}");
		}

		public GridData LoadFromAsset(out Tile[] tiles)
		{
			if (this.tiles == null || this.tiles.Length == 0)
			{
				tiles = null;
				Debug.LogError("MapData " + name + " has no stored Map!");
				return new GridData();
			}

			tiles = new Tile[this.tiles.Length];
			for (int i = 0; i < this.tiles.Length; i++) { tiles[i] = this.tiles[i]; }			
			return Data;
		}

		public bool UpdateTile(Tile tile)
        {
			if (!Grid.GetIndex(tile, data, out int index)) { return false; }

			tiles[index] = tile;
			EditorUtility.SetDirty(this);
			Debug.Log($"Tile {tile} updated in {name}");
			return true;
        }
	}
}