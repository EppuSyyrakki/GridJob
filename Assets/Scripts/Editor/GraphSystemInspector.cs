using System;
using UnityEditor;
using UnityEngine;

namespace GridJob
{
    [CustomEditor(typeof(GraphSystem))]
    public class GraphSystemInspector : Editor
    {	
        private bool editMode = false;

        private GraphSystem gs;
        private Tile original = Tile.MaxValue;
        private Tile edited = Tile.MaxValue;

		#region Unity messages and delegates
		private void OnEnable()
		{
			SceneView.duringSceneGui += DuringScene;
			gs = target as GraphSystem;
            if (!gs.Graph.IsInitialized) { gs.LoadGraph(); }
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
			if (editMode) { Selection.activeGameObject = gs.gameObject; }

			Event e = Event.current;

			// If not in edit mode or no L-click detected, exit.
			if (!editMode || e.type != EventType.MouseDown || e.button != 0) { return; }

			Vector3 mousePos = e.mousePosition;
			float ppp = EditorGUIUtility.pixelsPerPoint;
			mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
			mousePos.x *= ppp;
			Ray ray = scene.camera.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out var hit, 100f, gs.Graph.Data.terrainLayer))
			{
				Tile t = gs.Graph.WorldToTile(hit.point);   // Find the tile that was clicked

                if (!t.Equals(Tile.MaxValue) || !gs.Selected.Equals(t)) 
                {
                    PrintTile(t);
                    gs.Selected = t;
                    gs.SelectedChanged?.Invoke(t);                   
                }               
			}

			e.Use();
		}

        private void PrintTile(Tile t)
        {
            string s = $"Tile {t}, type {t.Type}. Edges: ";
            Tile[] directions = Tile.Directions_All;

            for (int i = 0; i < directions.Length; i++)
            {
                Edge e = Tile.DirectionToEdge(directions[i]);

                if (!t.HasAnyEdge(e)) { continue; }

                s += Enum.GetName(typeof(Edge), e) + ", ";
            }

            Debug.Log(s);
        }
        #endregion

        #region Draw Inspector
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (GUILayout.Button((editMode ? "Disable " : "Enable ") + "edit mode")) { editMode = !editMode; }
        }


        //private void DrawMainButtons()
        //{
        //	GUILayout.Space(10);
        //	GUILayout.Label("Grid creation");
        //	GUILayout.BeginHorizontal();

        //	if (GUILayout.Button("Create Map")) { GenerateMap(); }

        //	if (GUILayout.Button("Raycast Blocking Layers")) { RaycastBlocks(); }

        //	GUILayout.EndHorizontal();
        //}

        //private void DrawOperationButtons()
        //{
        //	GUILayout.Space(10);
        //	GUILayout.Label("MapData operations");
        //	GUILayout.BeginHorizontal();

        //	if (GUILayout.Button("Save MapData"))
        //	{
        //		if (!SaveMap(out var s)) { Debug.LogError(s); }
        //		else { Debug.Log(s); }

        //		EditorUtility.SetDirty(_grid.mapData);
        //	}

        //	if (GUILayout.Button("Load MapData"))
        //	{
        //		if (!LoadMap(out var s)) { Debug.LogError(s); }
        //		else { Debug.Log(s); }
        //	}

        //	if (GUILayout.Button("Erase MapData"))
        //	{
        //		if (EditorUtility.DisplayDialog("Erase saved MapData?",
        //			"This will clear all data from the scriptable object attached to this HexGrid."
        //			+ "This operation can't be undone.", "Erase", "Cancel"))
        //		{
        //			if (_grid.mapData == null)
        //			{
        //				Debug.LogError("Missing mapData object!");
        //				return;
        //			}

        //			_grid.mapData.EraseMapData();
        //			EditorUtility.SetDirty(_grid.mapData);
        //		}
        //	}

        //	EditorGUILayout.EndHorizontal();
        //}

        //private void DrawEditButtons()
        //{
        //	GUILayout.Space(10);
        //	GUILayout.Label("Map editing modes");

        //	GUILayout.BeginHorizontal();
        //	var oldColor = GUI.backgroundColor;
        //	GUI.backgroundColor = Color.red;

        //	if (editMode == HexEditMode.Delete)
        //	{
        //		if (GUILayout.Button("Disable Delete mode (Right click to delete Hex)")) { editMode = HexEditMode.None; }
        //	}

        //	if (editMode == HexEditMode.Edit)
        //	{
        //		if (GUILayout.Button("Disable Edit mode (Right click to edit Hex)")) { editMode = HexEditMode.None; }
        //	}

        //	if (editMode == HexEditMode.Add)
        //	{
        //		if (GUILayout.Button("Disable Add mode (Right click to add Hex)")) { editMode = HexEditMode.None; }
        //	}

        //	GUI.backgroundColor = oldColor;

        //	if (editMode == HexEditMode.None)
        //	{
        //		if (GUILayout.Button("Enable Edit mode")) { editMode = HexEditMode.Edit; }
        //		if (GUILayout.Button("Enable Delete mode")) { editMode = HexEditMode.Delete; }
        //		if (GUILayout.Button("Enable Add mode")) { editMode = HexEditMode.Add; }
        //	}

        //	EditorGUILayout.EndHorizontal();
        //}
        #endregion
    }
}