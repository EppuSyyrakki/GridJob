using GridJob.Jobs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GridJob
{
    public class GridEditor : MonoBehaviour
    {
        [SerializeField]
        bool logPathfinding = false, drawPathfinding = false, showAllTiles = false, showResult = false;

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

        private JobType scheduled = JobType.None;

        [SerializeField]
        private MapData unstoredData;
        
        private List<Tile> path;
        private List<Tile> field;

        public Graph Graph { get; private set; } 
        public MapAsset Asset => asset;

        public Tile Selected { get; set; }
        public Action<Tile> SelectedChanged;

        #region MonoBehaviour
        private void Awake()
        {
            Selected = Tile.MaxValue;
            path = new List<Tile>();
            field = new List<Tile>();
            player = GameObject.FindGameObjectWithTag("Player");

            if (!Graph.IsInitialized)
            {
                Load();
            }
        }

        private void OnEnable()
        {          
            n_tiles = new NativeArray<Tile>(Graph.Tiles.Length, Allocator.Persistent);
        }

        private void Start()
        {
            SetWorldPosition();

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

                if (enableDistanceField && ScheduleFieldJob(target, fieldRange))
                {
                    scheduled = JobType.Field;
                }
                else if (SchedulePathJob(start, target))
                {
                    scheduled = JobType.Path;
                }                
            }
        }

        private void LateUpdate()
        {
            if (scheduled == JobType.None) { return; }

            field.Clear();
            path.Clear();

            if (scheduled == JobType.Field)
            {
                fieldHandle.Complete();
                foreach (var tile in n_fieldResult) 
                { 
                    field.Add(tile); 
                }
                n_fieldResult.Dispose();
            }
            else if (scheduled == JobType.Path)
            {
                pathHandle.Complete();
                for (int i = 0; i < n_pathResult.Length; i++) 
                { 
                    path.Add(n_pathResult[i]); 
                }
                n_pathResult.Dispose();
            }

            scheduled = JobType.None;        
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

            if (field != null) { DrawResult(field, Color.green); }
            if (path != null) { DrawResult(path, Color.blue); }
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

        private bool SchedulePathJob(Tile start, Tile goal, int dropDepth = 1)
        {
            var mDist = Heuristic.Manhattan(goal, start, Graph.Data);
            var maxDistance = Mathf.Max(Graph.Data.size.x, Graph.Data.size.y) * Graph.Data.diagonalCost;

            if (logPathfinding)
            {
                Debug.Log($"Attempting to Schedule Path Job: From {start} To {goal}, " 
                    + $" Manhattan {mDist}, maxDist: {maxDistance}");
            }

            if (!Graph.HasTile(start, Graph.Data) || !Graph.HasTile(goal, Graph.Data) || mDist > maxDistance) { return false; }

            n_pathResult = new NativeList<Tile>(Graph.Data.maxPathLength, Allocator.TempJob);
            this.start = start;           
            var job = new AStarPathJob(start, goal, n_tiles, n_pathResult, Graph.Data, dropDepth, logPathfinding, drawPathfinding);
            pathHandle = job.Schedule();
            return true;
        }

        private bool ScheduleFieldJob(Tile center, int fieldRange, int dropDepth = 1)
        {           
            if (logPathfinding)
            {
                Debug.Log($"Attempting to schedule distance field job: From {center}, Dist: {fieldRange}" 
                    + $", MaxCost: {fieldRange * Graph.Data.directCost}");
            }

            if (!Graph.HasTile(center, Graph.Data) 
                || fieldRange > Mathf.Max(Graph.Data.size.x, Graph.Data.size.z)) { return false; }

            n_fieldResult = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            start = center;        
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, n_fieldResult, Graph.Data, dropDepth, logPathfinding, false);
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
            if (asset == null) { Debug.LogError(this + ": No Map Asset found!"); return; }
            else if (!asset.HasData) { Debug.LogError(this + ": Trying to load from empty Map Asset!"); return; }
            else if (!asset.Data.EnsureSize()) { Debug.LogError(this + ": Map Asset too large!."); }
            
            Graph = new Graph(asset, logPathfinding);      
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