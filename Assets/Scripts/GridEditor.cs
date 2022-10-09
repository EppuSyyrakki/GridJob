﻿using GridJob.Jobs;
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
        private GridAsset asset;

        [SerializeField]
        private Color gridColor = new Color(1, 1, 1, 0.08f);

        [SerializeField]
        private JobType jobType = JobType.None;

        [SerializeField, Range(0, 32)]
        private int fieldRange = 10;

        private GameObject player;
        private Tile start = Tile.zero;
        private JobHandle pathHandle;
        private JobHandle fieldHandle;
        private JobHandle fovHandle;
        private NativeList<Tile> n_pathResult;  // Allocator.TempJob
        private NativeList<Tile> n_fieldResult;   // Allocator.TempJob
        private NativeList<Tile> n_fovResult;   // Allocator.TempJob
        private NativeArray<Tile> n_tiles;  // Allocator.Persistent

        private JobType scheduled = JobType.None;

        [SerializeField]
        private GridData Data;
        
        private List<Tile> path;
        private List<Tile> field;
        private List<Tile> fov;

        public Grid Graph { get; private set; } 
        public GridAsset Asset => asset;
        public Tile Selected { get; set; }

        #region MonoBehaviour

        private void Awake()
        {
            path = new List<Tile>();
            field = new List<Tile>();
            fov = new List<Tile>();
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
            Tile playerPos = Graph.WorldToTile(player.transform.position);

            if (Grid.HasTile(playerPos, Graph.Data))
            {
                start = playerPos;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { fov.Clear(); return; }

                fov = Graph.LineCast2(start, target); //, TileType.WalkableTypes, false, true);
                Debug.DrawLine(Grid.TileToWorld(start, Graph.Data), Grid.TileToWorld(target, Graph.Data), Color.red, 10f);
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { return; }

                if (jobType == JobType.Field 
                    && ScheduleFieldJob(target, fieldRange))
                {
                    scheduled = JobType.Field;
                }
                else if (jobType == JobType.Path 
                    && SchedulePathJob(start, target))
                {
                    scheduled = JobType.Path;
                }
                else if (jobType == JobType.Fov 
                    && ScheduleFovJob(start, target - start, 80f))
                {
                    scheduled = JobType.Fov;
                }
            }
        }

        private void LateUpdate()
        {
            if (scheduled == JobType.None) { return; }

            if (scheduled == JobType.Field)
            {
                CompleteAndDispose(ref fieldHandle, ref n_fieldResult, field);
            }
            else if (scheduled == JobType.Path)
            {
                CompleteAndDispose(ref pathHandle, ref n_pathResult, path);
            }
            else if (scheduled == JobType.Fov)
            {
                CompleteAndDispose(ref fovHandle, ref n_fovResult, fov);
            }

            scheduled = JobType.None;        
        }

        private void OnDrawGizmosSelected()
        {
            if (showAllTiles) 
            {
                foreach (Tile t in Graph.Tiles)
                {
                    var world = Grid.TileToWorld(t, Graph.Data);
                    var draw = new Vector3(world.x, world.y + Graph.Data.cellSize.y * 0.5f, world.z);
                    var color = t.Equals(Selected) ? Color.green : gridColor;
                    Gizmos.color = t.Edges == Edge.None ? Color.black : color;
                    Gizmos.DrawWireCube(draw, Graph.Data.cellSize * 0.98f);
                }
            }

            if (!showResult) { return; }

            if (field != null) { DrawResult(field, Color.green); }
            if (path != null) { DrawResult(path, Color.blue); }
            if (fov != null) { DrawResult(fov, Color.magenta); }
        }

        private void DrawResult(List<Tile> tiles, Color color)
        {
            if (tiles == null) { return; }

            foreach (var tile in tiles)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(Grid.TileToWorld(tile, Graph.Data), Graph.Data.cellSize.x * 0.15f);
            }
        }

        #endregion

        #region Job scheduling

        private bool SchedulePathJob(Tile start, Tile goal, int dropDepth = 1)
        {
            var mDist = Heuristic.Manhattan(goal, start, Graph.Data);
            var maxDistance = math.max((int)Graph.Data.size.x, Graph.Data.size.y) * Graph.Data.diagonalCost;

            if (logPathfinding)
            {
                Debug.Log($"Attempting to Schedule Path Job: From {start} To {goal}, " 
                    + $" Manhattan {mDist}, maxDist: {maxDistance}");
            }

            if (!Grid.HasTile(start, Graph.Data) || !Grid.HasTile(goal, Graph.Data) || mDist > maxDistance) { return false; }

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

            if (!Grid.HasTile(center, Graph.Data) 
                || fieldRange > Mathf.Max(Graph.Data.size.x, Graph.Data.size.z)) { return false; }

            n_fieldResult = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            start = center;        
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, n_fieldResult, Graph.Data, dropDepth, logPathfinding, false);
            fieldHandle = job.Schedule();

            return true;
        }

        private bool ScheduleFovJob(Tile center, Tile forward, float angleWidth)
        {
            if (!Grid.HasTile(center, Graph.Data)) { return false; }

            n_fovResult = new NativeList<Tile>(Allocator.TempJob);
            start = center;
            var job = new FovJob(n_tiles, Graph.Data, n_fovResult, start, forward, angleWidth);
            fovHandle = job.Schedule();

            return true;
        }

        private void CompleteAndDispose(ref JobHandle handle, ref NativeList<Tile> from, List<Tile> to)
        {
            to.Clear();
            handle.Complete();

            for (int i = 0; i < from.Length; i++) { to.Add(from[i]); }

            from.Dispose();
        }

        #endregion

        private Tile MouseCastTile()
        {
            var mouse = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);
            Ray r = Camera.main.ScreenPointToRay(mouse);
            if (Physics.Raycast(r, out var hit))
            {
                return Graph.WorldToTile(hit.point);
            }

            return Tile.MaxValue;
        }

        public void LoadGraph(bool log = false)
        {
            if (asset == null) { Debug.LogError(this + ": No Map Asset found!"); return; }
            else if (!asset.HasData) { Debug.LogError(this + ": Trying to load from empty Map Asset!"); return; }
            else if (!asset.Data.EnsureSize()) { Debug.LogError(this + ": Map Asset too large!."); }
            
            Graph = new Grid(asset, log);
        }

        public void SetWorldPosition()
        {
            Graph.Data.SetWorldPosition(transform.position);
        }

        #region Custom inspector methods

        public void UpdateTile(Tile t, bool updateAsset)
        {
            string msg = "";
            if (updateAsset && !asset.UpdateTile(t)) 
            {
                Debug.LogError($"Could not update tile {t} - Not found in Asset {asset.name}");
                return;               
            }

            Graph.Tiles[t.index] = t;
            msg += $"Updated Grid with tile {t}";
            Debug.Log(msg);
        }

        [ContextMenu("Auto setup from unstored data", false, 0)]
        public void AutoSetup()
        {
            Graph = new Grid(Data, true);
            Graph.AutoBuild();           
        }  

        [ContextMenu("Save graph to asset", false, 1)]
        public void Save()
        {
            asset.SaveToAsset(Graph.Tiles, Graph.Data);
        }

        [ContextMenu("Force Load graph from asset", false, 2)]
        public void Load()
        {           
            LoadGraph(log: true);
            Data = Graph.Data;
        }
        #endregion
    }
}