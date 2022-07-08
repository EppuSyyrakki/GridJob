using System.Collections;
using UnityEngine;
using UnityEditor;

namespace Jobben
{
    [CustomEditor(typeof(TileEditor))]
    public class TileEditorInspector : Editor
    {
        private TileEditor editor;
        private Edge currentEdges;

        private void OnEnable()
        {
            editor = target as TileEditor;
        }

        public override void OnInspectorGUI()
        {
            if (editor.Tile.Equals(Tile.MaxValue)) { return; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected tile: ");
            EditorGUILayout.LabelField(editor.Tile.ToString());
            EditorGUILayout.EndHorizontal();
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }  
}
