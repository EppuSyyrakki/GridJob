using System.Collections;
using UnityEngine;
using UnityEditor;

namespace Jobben
{
    [ExecuteInEditMode]
    public class TileEditor : MonoBehaviour
    {
        [HideInInspector]
        private Tile original = Tile.MaxValue;
       
        private GraphSystem graphSystem;
        private Tile edited = Tile.MaxValue;

        public Tile Tile => edited;

        private void Awake()
        {
#if !UNITY_EDITOR
            Destroy(this);
            return;
#endif
            graphSystem = GetComponent<GraphSystem>();

            if (graphSystem == null) { Debug.LogError($"{gameObject} + has a Tile Editor but no GraphSystem"); }

            graphSystem.SelectedChanged += (t) => { original = t; edited = t; };
        }
    }
}