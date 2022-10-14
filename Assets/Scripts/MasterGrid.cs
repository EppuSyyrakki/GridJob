using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using GridSystem.Jobs;

namespace GridSystem
{
    public class MasterGrid : IDisposable
    {
        public MasterGrid(GridAsset asset)
        {
            this.asset = asset;
            Data = asset.Data;
            pathJobs = new Queue<GridJobItem>(MAX_QUEUE);
            fieldJobs = new Queue<GridJobItem>(MAX_QUEUE);
            fovJobs = new Queue<GridJobItem>(MAX_QUEUE);
            grid = new Grid(this.asset);
            n_tiles = new NativeArray<Tile>(grid.Tiles.Length, Allocator.Persistent);
            PopulateNativeTiles();
        }

        private const int MAX_QUEUE = 64;
        private GridAsset asset;
        private NativeList<Tile> n_path0; // result arrays = Allocator.TempJob
        private NativeList<Tile> n_path1;
        private NativeList<Tile> n_field0;
        private NativeList<Tile> n_field1;
        private NativeList<Tile> n_fov0;
        private NativeList<Tile> n_fov1;
        private JobHandle pathHandle0;
        private JobHandle pathHandle1;
        private JobHandle fieldHandle0;
        private JobHandle fieldHandle1;
        private JobHandle fovHandle0;
        private JobHandle fovHandle1;
        private GridJobItem path0;
        private GridJobItem path1;
        private GridJobItem field0;
        private GridJobItem field1;
        private GridJobItem fov0;
        private GridJobItem fov1;
        private NativeArray<Tile> n_tiles;  // Allocator.Persistent
        private Grid grid;

        // TODO: The queues need a better access implementation than just statics.
        private static Queue<GridJobItem> pathJobs;
        private static Queue<GridJobItem> fieldJobs;
        private static Queue<GridJobItem> fovJobs;

        public GridData Data { get; private set; }

        private void PopulateNativeTiles()
        {
            for (int i = 0; i < grid.Tiles.Length; i++) { n_tiles[i] = grid.Tiles[i]; }
        }

        private void DrawResult(List<Tile> tiles, Color color)
        {
            if (tiles == null) { return; }

            for (int i = 0; i < tiles.Count; i++)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(Grid.TileToWorld(tiles[i], grid.Data), grid.Data.cellSize.x * 0.15f);
            }
        }

        private JobHandle SchedulePathJob(Tile start, Tile goal, ref NativeList<Tile> result, int dropDepth)
        {
            if (!Grid.HasTile(start, grid.Data)) 
            {               
                throw new ArgumentOutOfRangeException(start.ToString(), $"{this}: Tile {start} not found!");
            }
            else if (!Grid.HasTile(goal, grid.Data))
            {
                throw new ArgumentOutOfRangeException(goal.ToString(), $"{this}: Tile {start} not found!");
            }

            result = new NativeList<Tile>(grid.Data.maxPathLength, Allocator.TempJob);
            var job = new AStarPathJob(start, goal, n_tiles, result, grid.Data, dropDepth);
            return job.Schedule();
        }

        private JobHandle ScheduleFieldJob(Tile center, int fieldRange, ref NativeList<Tile> result, int dropDepth)
        {
            if (!Grid.HasTile(center, grid.Data)) 
            {
                throw new ArgumentOutOfRangeException(center.ToString(), $"{this}: Tile {center} not found!");
            }

            result = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, result, grid.Data, dropDepth);
            return job.Schedule();
        }
        
        public void UpdateGrid()
        {

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
                if (grid.UpdateTile(t)) { n_tiles[t.index] = t; }
                else { return false; }
            }

            return true;
        }

        public void Dispose()
        {
            n_tiles.Dispose();
        }

        public static bool Request(GridJobItem request)
        {
            Queue<GridJobItem> queue;

            if (request.Type == JobType.Field) { queue = fieldJobs; }
            else if (request.Type == JobType.Path) { queue = pathJobs; }
            else if (request.Type == JobType.Fov) { queue = fovJobs; }
            else { return false; }

            if (queue.Count == MAX_QUEUE) { Debug.Log(request.Type + " queue is full, dropping request"); }

            queue.Enqueue(request);
            return true;
        }
    }
}