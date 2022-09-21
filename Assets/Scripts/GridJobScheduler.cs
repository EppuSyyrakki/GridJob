using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using GridJob.Jobs;

namespace GridJob
{
    public class GridJobScheduler : IDisposable
    {   
        private const int MAX_PATHJOBS = 4;
        private const int MAX_FIELDJOBS = 2;
        private const int MAX_PATH_QUEUE = 64;
        private const int MAX_FIELD_QUEUE = 32;
        private bool logPathfinding = false;
        private bool drawPathfinding = false;
        private MapAsset asset;
        private List<GridJobItem> pendingJobs;
        private NativeList<Tile> n_pathResult_0; // result arrays = Allocator.TempJob
        private NativeList<Tile> n_pathResult_1;
        private NativeList<Tile> n_pathResult_2;
        private NativeList<Tile> n_pathResult_3;
        private NativeList<Tile> n_fieldResult_0;
        private NativeList<Tile> n_fieldResult_1;
        private NativeList<JobHandle> pathHandles;   
        private NativeList<JobHandle> fieldHandles;
        private NativeArray<Tile> n_tiles;  // Allocator.Persistent

        private Graph graph { get; set; }       

        public static Queue<GridJobItem> PathQueue { get; private set; }
        public static Queue<GridJobItem> FieldQueue { get; private set; }
        public static MapData Data { get; private set; }

        public GridJobScheduler(bool log = false, bool draw = false)
        {
            logPathfinding = log;
            drawPathfinding = draw;
        }

        public void Init(MapAsset asset)
        {
            this.asset = asset;
            Data = asset.Data;
            PathQueue = new Queue<GridJobItem>(MAX_PATH_QUEUE);
            FieldQueue = new Queue<GridJobItem>(MAX_FIELD_QUEUE);
            graph = new Graph(this.asset, logPathfinding);
            n_tiles = new NativeArray<Tile>(graph.Tiles.Length, Allocator.Persistent);
            PopulateNativeTiles();
        }

        private void PopulateNativeTiles()
        {
            for (int i = 0; i < graph.Tiles.Length; i++) { n_tiles[i] = graph.Tiles[i]; }
        }

        private void DrawResult(List<Tile> tiles, Color color)
        {
            if (tiles == null) { return; }

            for (int i = 0; i < tiles.Count; i++)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(Graph.TileToWorld(tiles[i], graph.Data), graph.Data.cellSize.x * 0.15f);
            }
        }

        private JobHandle SchedulePathJob(Tile start, Tile goal, ref NativeList<Tile> result)
        {
            var mDist = Heuristic.Manhattan(goal, start, graph.Data);
            var maxDistance = Mathf.Max(graph.Data.size.x, graph.Data.size.y) * graph.Data.diagonalCost;

            if (logPathfinding)
            {
                Debug.Log($"Attempting to Schedule Path Job: From {start} To {goal}, "
                    + $" Manhattan {mDist}, maxDist: {maxDistance}");
            }

            if (!Graph.HasTile(start, graph.Data)) 
            {               
                throw new ArgumentOutOfRangeException(start.ToString(), $"{this}: Tile {start} not found!");
            }
            else if (!Graph.HasTile(goal, graph.Data))
            {
                throw new ArgumentOutOfRangeException(goal.ToString(), $"{this}: Tile {start} not found!");
            }

            result = new NativeList<Tile>(graph.Data.maxPathLength, Allocator.TempJob);
            var job = new AStarPathJob(start, goal, n_tiles, result, graph.Data, logPathfinding, drawPathfinding);
            return job.Schedule();
        }

        private JobHandle ScheduleFieldJob(Tile center, int fieldRange, ref NativeList<Tile> result)
        {
            if (logPathfinding)
            {
                Debug.Log($"Attempting to schedule distance field job: From {center}, Dist: {fieldRange}"
                    + $", MaxCost: {fieldRange * graph.Data.directCost}");
            }

            if (!Graph.HasTile(center, graph.Data)) 
            {
                throw new ArgumentOutOfRangeException(center.ToString(), $"{this}: Tile {center} not found!");
            }

            result = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, result, graph.Data, logPathfinding);
            return job.Schedule();
        }        

        public void ScheduleJobs()
        {
        }

        public void CompleteJobs()
        {
        }

        public bool UpdateTiles(List<Tile> updated)
        {
            foreach (Tile t in updated)
            {
                if (graph.UpdateTile(t)) { n_tiles[t.index] = t; }
                else { return false; }
            }

            return true;
        }

        public void Dispose()
        {
            n_tiles.Dispose();
        }
    }
}