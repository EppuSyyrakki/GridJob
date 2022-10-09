using System;
using UnityEditor;
using UnityEngine;

namespace GridJob
{
    [CustomEditor(typeof(GridEditor))]
    public class GridEditorInspector : Editor
    {	
        private GridEditor ge;
        private Tile original = Tile.MaxValue;
        private Tile edited = Tile.MaxValue;
        private bool EditMode = false;

		#region Unity messages and delegates

		private void OnEnable()
		{
			SceneView.duringSceneGui += DuringScene;
			ge = target as GridEditor;
            if (!ge.Graph.IsInitialized) { ge.LoadGraph(); }
        }

		private void OnDisable()
		{
			SceneView.duringSceneGui -= DuringScene;
        }

		/// <summary>
		/// Delegate method that is called on editor update.
		/// </summary>
		private void DuringScene(SceneView scene)
		{
			// If we're editing the grid, make sure gameObject is the active one (ignore clicks on others)
			if (EditMode) 
            {
                //if (!original.Equals(Tile.MaxValue)) 
                //{
                //    var world = Graph.TileToWorld(original, gs.Graph.Data);
                //    var draw = new Vector3(world.x, world.y + gs.Graph.Data.cellSize.y * 0.5f, world.z);
                //    Gizmos.color = Color.green;
                //    Gizmos.DrawWireCube(draw, gs.Graph.Data.cellSize * 0.95f);
                //}

                Selection.activeGameObject = ge.gameObject; 
            }

			Event e = Event.current;

			// If not in edit mode or no L-click detected, exit.
			if (!EditMode || e.type != EventType.MouseDown || e.button != 0) { return; }

			Vector3 mousePos = e.mousePosition;
			float ppp = EditorGUIUtility.pixelsPerPoint;
			mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
			mousePos.x *= ppp;
			Ray ray = scene.camera.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out var hit, 100f, ge.Graph.Data.AllLayers))
			{
				Tile t = ge.Graph.WorldToTile(hit.point);   // Find the tile that was clicked

                if (!t.Equals(Tile.MaxValue) || !original.Equals(t)) 
                {                   
                    original = t;
                    edited = t;
                    Repaint();
                    ge.Selected = original;
                }               
			}

			e.Use();
		}

        #endregion

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(10);
            GUILayout.Label("Asset operations");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate from Data")) 
            { 
                ge.AutoSetup();
                Repaint();
            }

            if (GUILayout.Button("Save to Asset"))
            {
                if ((ge.Asset.Tiles.Length > 0) && EditorUtility.DisplayDialog("Overwrite asset?",
                    "This will overwrite the Map Asset " + ge.Asset.name +
                    "This operation can't be undone.", "Overwrite", "Cancel"))
                {
                    ge.Save();
                }
            }

            if (GUILayout.Button("Load from Asset"))
            {
                ge.Load();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);

            if (GUILayout.Button((EditMode ? "Disable " : "Enable ") + "edit mode")) 
            { 
                EditMode = !EditMode;
                Repaint();
            }

            if (!EditMode) { return; }
           
            GUILayout.Space(10);
            GUILayout.Label("Tile edit");
            GUILayout.Label("Selected: " + original);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Edges: ");
            edited.SetEdges((Edge)EditorGUILayout.EnumFlagsField(original.Edges));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Covers: ");
            edited.SetCovers((Cover)EditorGUILayout.EnumFlagsField(original.Covers));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Types: ");
            edited.SetType((TileType)EditorGUILayout.EnumFlagsField(original.Type));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Update tile")) 
            {
                int i = EditorUtility.DisplayDialogComplex("Update tile", "Updating tile - select destination:",
                    "Grid only", "Grid and Asset", "Cancel");

                if (i == 0) { ge.UpdateTile(edited, updateAsset: false); }
                else if (i == 1) { ge.UpdateTile(edited, updateAsset: true); }
                else { return; }
            }
            if(GUILayout.Button("Cancel changes")) { edited = original; }
            EditorGUILayout.EndHorizontal();
        }
    }
}