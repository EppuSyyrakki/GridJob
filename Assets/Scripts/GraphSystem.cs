using Jobben.Jobs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Jobben
{
    public class GraphSystem : MonoBehaviour
    {
        [SerializeField]
        bool logPathfinding = false, drawPathfinding = false, showAllTiles = false, showResult = false;

        [SerializeField]
        private int jobCount = 1;

        [SerializeField]
        private MapAsset asset;

        [SerializeField]
        private Color gridColor = new Color(1, 1, 1, 0.08f);

        [SerializeField]
        private bool enableDistanceField = true;

        [SerializeField, Range(0, 32)]
        private int fieldRange = 10;


        private GameObject player;
        private Tile start = Tile.zero;
        private JobHandle pathHandle;
        private JobHandle fieldHandle;
        private NativeList<Tile> n_pathResult;  // Allocator.TempJob
        private NativeList<Tile> n_fieldResult;   // Allocator.TempJob
        private NativeArray<Tile> n_tiles;  // Allocator.Persistent

        private bool scheduled = false;

        [SerializeField]
        private MapData unstoredData;
        
        private List<Tile> aStarPath;
        private List<Tile> bfsField;

        public Graph Graph { get; private set; } 
        public MapAsset Asset => asset;

        public Tile Selected { get; set; }
        public Action<Tile> SelectedChanged;

        #region MonoBehaviour
        private void Awake()
        {
            Selected = Tile.MaxValue;
            aStarPath = new List<Tile>();
            bfsField = new List<Tile>();
            player = GameObject.FindGameObjectWithTag("Player");

            if (!Graph.IsInitialized)
            {
                LoadGraph();
            }
        }

        private void OnEnable()
        {          
            n_tiles = new NativeArray<Tile>(Graph.Tiles.Length, Allocator.Persistent);
        }

        private void Start()
        {
            SetWorldPosition();
            AutoSetup();

            for (int i = 0; i < Graph.Tiles.Length; i++)
            {
                n_tiles[i] = Graph.Tiles[i];
            }
        }

        private void OnDisable()
        {
            n_tiles.Dispose();
        }

        private void Update()
        {
            SetWorldPosition();
            Tile playerPos = Graph.WorldToTile(player.transform.position);

            if (Graph.HasTile(playerPos, Graph.Data))
            {
                start = playerPos;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { return; }               

                //ScheduleFieldJob(start, fieldRange); 
                SchedulePathJob(start, target);
                scheduled = true;
            }
        }

        private void LateUpdate()
        {
            if (!scheduled) { return; }

            bfsField.Clear();
            aStarPath.Clear();
            // bfsHandle.Complete();            
            pathHandle.Complete();

            // foreach (var tile in n_bfsResult) { bfsField.Add(tile); }
            for (int i = 0; i < n_pathResult.Length; i++) { aStarPath.Add(n_pathResult[i]); }

            // n_bfsResult.Dispose();
            n_pathResult.Dispose();
            scheduled = false;        
        }

        private void OnDrawGizmosSelected()
        {
            if (showAllTiles) 
            {
                foreach (Tile t in Graph.Tiles)
                {
                    var world = Graph.TileToWorld(t, Graph.Data);
                    var draw = new Vector3(world.x, world.y + Graph.Data.cellSize.y * 0.5f, world.z);
                    var color = t.Equals(Selected) ? Color.green : gridColor;
                    Gizmos.color = t.Edges == Edge.None ? Color.black : color;
                    Gizmos.DrawWireCube(draw, Graph.Data.cellSize * 0.98f);
                }
            }

            if (!showResult) { return; }

            if (bfsField != null) { DrawResult(bfsField, Color.green); }
            if (aStarPath != null) { DrawResult(aStarPath, Color.blue); }
        }

        private void DrawResult(List<Tile> tiles, Color color)
        {
            if (tiles == null) { return; }

            for (int i = 0; i < tiles.Count; i++)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(Graph.TileToWorld(tiles[i], Graph.Data), Graph.Data.cellSize.x * 0.15f);
            }
        }
        #endregion

        private bool SchedulePathJob(Tile start, Tile goal)
        {
            var mDist = Heuristic.Manhattan(goal, start, Graph.Data);
            var maxDistance = Graph.Data.maxPathLength * Graph.Data.directCost;

            if (logPathfinding)
            {
                Debug.Log($"Attempting to Schedule Path Job: From {start} To {goal}, " 
                    + $" Manhattan {mDist}, maxDist: {maxDistance}");
            }

            if (!Graph.HasTile(start, Graph.Data) || !Graph.HasTile(goal, Graph.Data) || mDist > maxDistance) { return false; }

            n_pathResult = new NativeList<Tile>(Graph.Data.maxPathLength, Allocator.TempJob);
            this.start = start;           
            var job = new AStarPathJob(start, goal, n_tiles, n_pathResult, Graph.Data, logPathfinding, drawPathfinding);
            pathHandle = job.Schedule();
            return true;
        }

        private bool ScheduleFieldJob(Tile center, int fieldRange)
        {           
            if (logPathfinding)
            {
                Debug.Log($"Attempting to schedule distance field job: From {center}, Dist: {fieldRange}" 
                    + $", MaxCost: {fieldRange * Graph.Data.directCost}");
            }

            if (!Graph.HasTile(center, Graph.Data) 
                || fieldRange > math.max(Graph.Data.size.x, Graph.Data.size.z)) { return false; }

            n_fieldResult = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            start = center;        
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, n_fieldResult, Graph.Data, logPathfinding);
            fieldHandle = job.Schedule();

            return true;
        }

        private Tile MouseCastTile()
        {
            Ray ray = MouseRay(new Vector2(Input.mousePosition.x, Input.mousePosition.y));
            Physics.Raycast(ray, out var hit);
            return Graph.WorldToTile(hit.point);
        }

        public void LoadGraph()
        {
            if (asset.Data.EnsureSize())
            {
                unstoredData = asset.Data;
                Graph = new Graph(asset, logPathfinding);
                return;
            }

            Debug.LogError(this + " Graph asset loading failed.");
        }

        private Ray MouseRay(float2 mousePos)
        {
            var pos = new Vector3(mousePos.x, mousePos.y, 0);
            return Camera.main.ScreenPointToRay(pos);
        }

        public void SetWorldPosition()
        {
            Graph.Data.SetWorldPosition(transform.position);
        }

        public void UpdateTile(Tile t)
        {
            if (!asset.UpdateTile(t)) { return; }
            LoadGraph();
        }

        [ContextMenu("Generate new from unstored data", false, 0)]
        private void GenerateGraph()
        {
            Graph = new Graph(unstoredData, true);
        }

        [ContextMenu("Auto setup tile connections", false, 1)]
        private void AutoSetup()
        {
            Graph.AutoBuild();           
        }  

        [ContextMenu("Save graph to asset", false, 2)]
        private void Save()
        {
            asset.SaveToAsset(Graph.Tiles, Graph.Data);
        }

        [ContextMenu("Force Load graph from asset", false, 3)]
        private void Load()
        {
            LoadGraph();
        }
    }
}