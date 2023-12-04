using Unity.Mathematics;
using UnityEngine;

namespace GridSystem
{
    [System.Serializable]
    public struct GridData
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
        [SerializeField]
        public LayerMask blockedLayers;
        [SerializeField]
        public LayerMask climbLayers;

        [HideInInspector]
        public Vector3 transformPosition;

        public int Length => size.x * size.y * size.z;
        public LayerMask AllLayers => blockedLayers | climbLayers;

        public bool EnsureSize()
        {
            bool ensured = size.x > 0 && size.y > 0 && size.z > 0 && Length <= MAX_LENGTH;
#if UNITY_EDITOR
            if (!ensured) { Debug.LogError("Invalid size in Grid Data!"); }
#endif
            return ensured; 
        }

        public void SetWorldPosition(Vector3 pos)
        {
            transformPosition = pos;
        }

        public static bool EnsureSize(int length)
        {
            return length <= MAX_LENGTH;
        }
    }	
}