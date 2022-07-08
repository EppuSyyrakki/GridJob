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
        bool log = true, showNodes = true;

        [SerializeField]
        private int jobCount = 1;

        [SerializeField]
        private MapAsset asset;

        [SerializeField]
        private Color gridColor = new Color(1, 1, 1, 0.08f);

        private NativeArray<JobHandle> n_handles;   // Allocator.TempJob
        private NativeArray<Tile> n_starts; // Allocator.TempJob
        private NativeArray<Tile> n_goals;  // Allocator.TempJob         
        private NativeList<Tile> n_result;  // Allocator.TempJob
        private NativeArray<Tile> n_tiles;  // Allocator.Persistent

        private bool scheduled = false;
        private float scheduleTime;

        [SerializeField]
        private MapData data;
        private List<Tile> path;

        public Graph Graph { get; private set; } 
        public MapAsset Asset => asset;
        public List<Tile> Path => path;

        public Tile Selected { get; set; }
        public Action<Tile> SelectedChanged;

        #region MonoBehaviour
        private void Awake()
        {
            Selected = Tile.MaxValue;
            path = new List<Tile>();

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
        }

        private void OnDisable()
        {
            n_tiles.Dispose();
        }

        private void Update()
        {
            SetWorldPosition();

            if (Input.GetMouseButtonDown(0))
            {
                InitTempJobNatives();
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { return; }

                for (int i = 0; i < Graph.Tiles.Length; i++)
                {
                    n_tiles[i] = Graph.Tiles[i];
                }

                for (int i = 0; i < jobCount; i++)
                {
                    if (ScheduleJob(new Tile(11, 2, 3), target, i))
                    {
                        // TODO: Something?
                    }
                }

                scheduled = true;
                scheduleTime = Time.time;
            }
        }

        private void LateUpdate()
        {
            if (!scheduled) { return; }

            JobHandle.CompleteAll(n_handles);
            path.Clear();

            for (int i = 0; i < n_result.Length; i++)
            {
                path.Add(n_result[i]);
            }

            if (log) { Debug.Log($"Completed {jobCount} PathJobs in {Time.time - scheduleTime} "); path.Clear(); }

            DisposeTempJobNatives();
            scheduled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showNodes) { return; }

            foreach (Tile t in Graph.Tiles)
            {               
                var world = Graph.TileToWorld(t, Graph.Data);
                var draw = new Vector3(world.x, world.y + Graph.Data.cellSize.y * 0.5f, world.z);
                var color = t.Equals(Selected) ? Color.green : gridColor;
                Gizmos.color = t.Edges == Edge.None ? Color.black : color;
                Gizmos.DrawWireCube(draw, Graph.Data.cellSize * 0.98f);
            }

            if (path != null && path.Count > 0)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(Graph.TileToWorld(path[i], Graph.Data), Graph.Data.cellSize.x * 0.25f);
                }
            }
        }
        #endregion

        private bool ScheduleJob(Tile start, Tile goal, int i)
        {
            if (!Graph.HasTile(start, Graph.Data.size) || !Graph.HasTile(goal, Graph.Data.size))
            {
                return false;
            }

            n_starts[i] = start;
            n_goals[i] = goal;
            var job = new PathJob(n_starts[i], n_goals[i], n_tiles, n_result, data, log);
            JobHandle dependsOn = i == 0 ? default : n_handles[i - 1];
            n_handles[i] = job.Schedule(dependsOn);

            if (log)
            {
                var mDist = Graph.ManhattanDistance(n_goals[i], n_starts[i], Graph.Data);
                Debug.Log($"Job Scheduled: From {n_starts[i]} To {n_goals[i]},  Manhattan {mDist}, ");
            }

            return true;
        }

        private Tile MouseCastTile()
        {
            Ray ray = MouseRay(new Vector2(Input.mousePosition.x, Input.mousePosition.y));
            Physics.Raycast(ray, out var hit);
            return Graph.WorldToNode(hit.point);
        }

        private void InitTempJobNatives()
        {
            n_starts = new NativeArray<Tile>(jobCount, Allocator.TempJob);
            n_goals = new NativeArray<Tile>(jobCount, Allocator.TempJob);
            n_handles = new NativeArray<JobHandle>(jobCount, Allocator.TempJob);
            n_result = new NativeList<Tile>(Graph.Data.maxPathLength, Allocator.TempJob);
        }

        private void DisposeTempJobNatives()
        {
            n_starts.Dispose();
            n_goals.Dispose();
            n_handles.Dispose();
            n_result.Dispose();
        }

        private static Tile Random(int3 size)
        {
            var data = new int3(UnityEngine.Random.Range(0, size.x), UnityEngine.Random.Range(0, size.y), UnityEngine.Random.Range(0, size.z));
            return new Tile(data);
        }

        public void LoadGraph()
        {
            if (asset.Data.EnsureSize())
            {
                data = asset.Data;
                Graph = new Graph(asset, log);
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

        [ContextMenu("Auto setup node connections", false, 0)]
        private void AutoSetup()
        {
            Graph.AutoBuild();           
        }

        [ContextMenu("Generate new from this data", false, 1)]
        private void GenerateGraph()
        {
            Graph = new Graph(data, true);
        }

        [ContextMenu("Save graph to asset and reload", false, 2)]
        private void Save()
        {
            asset.SaveToAsset(Graph.Tiles, Graph.Data);
            LoadGraph();
        }
    }
}