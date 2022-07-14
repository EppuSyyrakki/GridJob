using Unity.Mathematics;
using UnityEngine;

namespace Jobben
{
    [System.Serializable]
    public struct MapData
    {
        public const int MAX_LENGTH = 65536;

        [SerializeField]
        public byte3 size;
        [SerializeField]
        public int directCost, diagonalCost, upCost;
        [SerializeField]
        public int maxPathLength;
        [SerializeField]
        public Vector3 cellSize;
        [SerializeField, Range(0.1f, 0.5f), Tooltip("Size of the Boxcast that tries to detect obstacles inside nodes.")]
        public float obstacleCastRadius;
        [SerializeField, Header("Layer info")]
        public LayerMask coverLayer, terrainLayer, climbLayer, structureLayer;

        [HideInInspector]
        public Vector3 transformPosition;

        public int Length => size.x * size.y * size.z;

        public bool EnsureSize()
        {
            return size.x > 0 && size.y > 0 && size.z > 0 && Length <= MAX_LENGTH;
        }

        public void SetWorldPosition(Vector3 pos)
        {
            transformPosition = pos;
        }

        public static TileType LayerMapping(int layer, MapData data)
        {
            if ((1 << layer & data.terrainLayer) > 0) { return TileType.Terrain; }
            if ((1 << layer & data.structureLayer) > 0) { return TileType.Structure; }
            if ((1 << layer & data.coverLayer) > 0) { return TileType.Cover; }
            if ((1 << layer & data.climbLayer) > 0) { return TileType.Climb; }
            return TileType.Empty;
        }
    }	
}