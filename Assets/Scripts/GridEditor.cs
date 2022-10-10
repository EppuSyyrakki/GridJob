using GridSystem.Jobs;
using System;
using System.Collections.Generic;
using System.Dynamic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace GridSystem
{
    public class GridEditor : MonoBehaviour
    {
        [Flags]
        public enum Helpers : byte
        { 
            None = 0, 
            LogPathfinder = 1 << 0, 
            DrawPathfinder = 1 << 1, 
            DrawResult = 1 << 2, 
            DrawAllTiles = 1 << 3, 
            DrawSelectedTile = 1 << 4 }

        [SerializeField]
        private GridAsset asset;

        [SerializeField]
        private Helpers helpers;       

        [SerializeField]
        private Color gridColor = new Color(1, 1, 1, 0.08f), selectedColor = Color.green;

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

        public Grid GridMap { get; private set; } 
        public GridAsset Asset => asset;
        public Tile Selected { get; set; }

        #region MonoBehaviour

        private void Awake()
        {
            path = new List<Tile>();
            field = new List<Tile>();
            fov = new List<Tile>();
            player = GameObject.FindGameObjectWithTag("Player");

            if (!GridMap.IsInitialized)
            {
                ForceLoad();
            }
        }

        private void OnEnable()
        {          
            n_tiles = new NativeArray<Tile>(GridMap.Tiles.Length, Allocator.Persistent);
        }

        private void Start()
        {
            GridMap.Data.SetWorldPosition(transform.position);

            for (int i = 0; i < GridMap.Tiles.Length; i++)
            {
                n_tiles[i] = GridMap.Tiles[i];
            }
        }

        private void OnDisable()
        {
            n_tiles.Dispose();
        }

        private void Update()
        {            
            Tile playerPos = GridMap.WorldToTile(player.transform.position);

            if (Grid.HasTile(playerPos, GridMap.Data))
            {
                start = playerPos;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { fov.Clear(); return; }

                fov = GridMap.Linecast(start, target, TileType.MovableTypes);
                Debug.DrawLine(Grid.TileToWorld(start, GridMap.Data), Grid.TileToWorld(target, GridMap.Data), Color.red, 10f);
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
            if ((helpers & Helpers.DrawAllTiles) > 0)
            {
                foreach (Tile t in GridMap.Tiles) { WireCube(t, t.Edges == Edge.None ? Color.black : gridColor); }
            }

            if ((helpers & Helpers.DrawSelectedTile) > 0)
            {
                if (!Selected.Equals(Tile.MaxValue)) { WireCube(Selected, selectedColor); }
            }

            if ((helpers & Helpers.DrawResult) > 0) 
            { 
                if (field != null) { DrawResult(field, Color.green); }
                if (path != null) { DrawResult(path, Color.blue); }
                if (fov != null) { DrawResult(fov, Color.magenta); }
            }

            void WireCube(Tile t, Color c)
            {
                var world = Grid.TileToWorld(t, GridMap.Data);
                var draw = new Vector3(world.x, world.y + GridMap.Data.cellSize.y * 0.5f, world.z);
                Gizmos.color = c;
                Gizmos.DrawWireCube(draw, GridMap.Data.cellSize * 0.98f);
            }
        }


        private void DrawResult(List<Tile> tiles, Color color)
        {
            if (tiles == null) { return; }

            foreach (var tile in tiles)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(Grid.TileToWorld(tile, GridMap.Data), GridMap.Data.cellSize.x * 0.15f);
            }
        }

        #endregion

        #region Job scheduling

        private bool SchedulePathJob(Tile start, Tile goal, int dropDepth = 1)
        {
            var mDist = Heuristic.Manhattan(goal, start, GridMap.Data);
            int largest = math.max(GridMap.Data.size.x, math.max((int)GridMap.Data.size.y, GridMap.Data.size.z));
            var maxDistance = largest * 2 * GridMap.Data.diagonalCost;
            bool log = (helpers & Helpers.LogPathfinder) > 0;
            bool draw = (helpers & Helpers.DrawPathfinder) > 0;

            if (log)
            {
                Debug.Log($"Attempting to Schedule Path Job: From {start} To {goal}, " 
                    + $" Manhattan {mDist}, maxDist: {maxDistance}");
            }

            if (!Grid.HasTile(start, GridMap.Data) || !Grid.HasTile(goal, GridMap.Data) || mDist > maxDistance) { return false; }

            n_pathResult = new NativeList<Tile>(GridMap.Data.maxPathLength, Allocator.TempJob);
            this.start = start;           
            var job = new AStarPathJob(start, goal, n_tiles, n_pathResult, GridMap.Data, dropDepth);
            pathHandle = job.Schedule();
            return true;
        }

        private bool ScheduleFieldJob(Tile center, int fieldRange, int dropDepth = 1)
        {
            bool log = (helpers & Helpers.LogPathfinder) > 0;

            if (log)
            {
                Debug.Log($"Attempting to schedule distance field job: From {center}, Dist: {fieldRange}" 
                    + $", MaxCost: {fieldRange * GridMap.Data.directCost}");
            }

            if (!Grid.HasTile(center, GridMap.Data) 
                || fieldRange > Mathf.Max(GridMap.Data.size.x, GridMap.Data.size.z)) { return false; }

            n_fieldResult = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            start = center;        
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, n_fieldResult, GridMap.Data, dropDepth, false);
            fieldHandle = job.Schedule();

            return true;
        }

        private bool ScheduleFovJob(Tile center, Tile forward, float angleWidth)
        {
            if (!Grid.HasTile(center, GridMap.Data)) { return false; }

            n_fovResult = new NativeList<Tile>(Allocator.TempJob);
            start = center;
            var job = new FovJob(n_tiles, GridMap.Data, n_fovResult, start, forward, angleWidth);
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
                return GridMap.WorldToTile(hit.point);
            }

            return Tile.MaxValue;
        }

        private void LoadGraph(bool log = false)
        {
            if (asset == null) { Debug.LogError(this + ": No Map Asset found!"); return; }
            else if (!asset.HasData) { Debug.LogError(this + ": Trying to load from empty Map Asset!"); return; }
            else if (!asset.Data.EnsureSize()) { Debug.LogError(this + ": Map Asset too large!."); }
            
            GridMap = new Grid(asset, log);
        }

        #region Grid building
        public static Tile[] AutoBuild(Tile[] tiles, GridData data)
        {
            LayerMask layers = data.terrainLayer | data.climbLayer;

            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = tiles[i];
                tile.RemoveEdges(Edge.All);
                tile.SetType(BoxcastTileType(tile, layers, data, includeTriggers: true));
                tile.AddCovers(LinecastCover(in tiles, tile, data));
                tiles[i] = tile;
            }

            // Tiles have no edges as default. Add them according to type and neighbors.
            Grid.GetTileTypes(in tiles, TileType.Empty, TileType.Climb, out var empties, out var climbs);

            foreach (Tile tile in empties)
            {
                Tile altered = HandleEmptys(in tiles, tile, data);
                tiles[altered.index] = altered;
            }

            foreach (Tile tile in climbs)
            {
                Tile altered = HandleClimb(ref tiles, tile, data);
                tiles[altered.index] = altered;
            }

            return tiles;
        }

        private static Tile HandleEmptys(in Tile[] tiles, Tile tile, GridData data)
        {
            if (tile.data.y == 0)	// Bottom floor, no need to check for neighbor below this
            {
                tile = AddLateralEdgesTowardNeighbors(in tiles, tile, data);
                return tile;
            }

            if (!Grid.GetIndex(tile + Tile.down, data, out int belowIndex)) { return tile; }

            Tile below = tiles[belowIndex]; // A tile exists below this one

            if (tile.HasNoCover(Cover.Down) && below.IsAnyType(TileType.MovableTypes))  // Tile below is free
            {
                tile.AddEdges(Edge.Down);
                tile.SetType(TileType.Jump);
                return tile;
            }

            tile = AddLateralEdgesTowardNeighbors(in tiles, tile, data);    // Tile below is blocked
            return tile;
        }

        private static Tile AddLateralEdgesTowardNeighbors(in Tile[] tiles, Tile tile, GridData data)
        {
            var directions = Tile.Directions_Lateral;

            for (int i = 0; i < directions.Length; i++) // loop through all lateral neighbors
            {
                Edge e = Tile.DirectionToEdge(directions[i]);

                if (tile.HasAnyCover((Cover)e)) { continue; }

                if (Grid.GetIndex(tile + directions[i], data, out int neighborIndex))
                {
                    Tile neighbor = tiles[neighborIndex];

                    if (neighbor.IsAnyType(TileType.BlockedTypes)) { continue; }

                    tile.AddEdges(e); // not blocked, add edge
                }
            }

            directions = Tile.Directions_Diagonal;

            for (int i = 0; i < directions.Length; i++) // loop diagonal neighbors
            {
                if (!Grid.GetIndex(tile + directions[i], data, out int dirIndex)) { continue; }

                Edge e = Tile.DirectionToEdge(directions[i]);
                var adjEdges = e.Adjacents();
                Tile neighbor = tiles[dirIndex];
                var adjTiles = Tile.Adjacents(directions[i]);

                if (!tile.HasPassageTo(adjEdges.e1)
                    || !tile.HasPassageTo(adjEdges.e2)
                    || !CheckAdjacentToNeighbor(in tiles, in adjTiles.t1, in neighbor) 
                    || !CheckAdjacentToNeighbor(in tiles, in adjTiles.t2, in neighbor))
                {
                    tile.RemoveEdges(e);
                }
            }

            return tile;

            bool CheckAdjacentToNeighbor(in Tile[] tiles, in Tile adjDir, in Tile neighbor)
            {
                if (!Grid.GetIndex(tile + adjDir, data, out int adjIndex)) { return false; }
                
                Tile adjacent = tiles[adjIndex];
                Edge adjToDir = Tile.DirectionToEdge(neighbor - adjacent);  // dir adjacent -> original neighbor

                if (adjacent.HasAnyCover((Cover)adjToDir)) { return false; }

                return true;
            }
        }

        /// <summary> Enables edges according to adjacents and their adjacents on Climb tiles</summary>
        private static Tile HandleClimb(ref Tile[] tiles, Tile tile, GridData data)
        {
            if (Grid.GetIndex(tile + Tile.up, data, out int aboveIndex)) // Is there a tile above this one?
            {
                bool tileHasUpCover = tile.HasAnyCover(Cover.Up);
                if (!tileHasUpCover) { tile.AddEdges(Edge.Up); }  // Only add up edge if there's no cover

                Tile above = tiles[aboveIndex];

                if (above.IsAnyType(TileType.Jump) && !tileHasUpCover)
                {
                    var directions = Tile.Directions_Direct;    // Only scan (N E S W) exiting climb tiles 

                    for (int i = 0; i < directions.Length; i++)
                    {
                        // Loop neighbors, find any blocked and add the direction to that as edge to above tile
                        if (!Grid.GetIndex(tile + directions[i], data, out int neighborIndex)) { continue; }

                        Tile neighbor = tiles[neighborIndex];  // Neighbor exists.
                        Tile aboveNeighbor = tiles[Grid.GetIndex(neighbor + Tile.up, data.size)];

                        if (neighbor.IsAnyType(TileType.BlockedTypes) || aboveNeighbor.HasAnyCover(Cover.Down))
                        {
                            // dir is blocked or above's neighbor has "floor", above can move there
                            Edge edge = Tile.DirectionToEdge(directions[i]);
                            above.AddEdges(edge);
                            tiles[above.index] = above;
                        }
                    }
                }
            }

            if (Grid.GetIndex(tile + Tile.down, data, out int belowIndex))
            {
                Tile below = tiles[belowIndex]; // A tile below exists

                if (tile.HasAnyCover(Cover.Down) || below.IsAnyType(TileType.BlockedTypes))
                {
                    tile = AddLateralEdgesTowardNeighbors(in tiles, tile, data);    // climb tiles above blockeds can be exited in any dir
                }
                else if (tile.HasNoCover(Cover.Down) && below.IsAnyType(TileType.MovableTypes))
                {
                    tile.AddEdges(Edge.Down);   // climb tiles over emptys can only be travelled down

                    var directions = Tile.Directions_Direct;

                    foreach (var dir in directions)
                    {
                        if (!Grid.GetIndex(below + dir, data, out int belowDirIndex)) { continue; }

                        Tile belowNeighbor = tiles[belowDirIndex];
                        Tile tileNeighbor = tiles[Grid.GetIndex(tile + dir, data.size)];

                        if ((belowNeighbor.IsAnyType(TileType.BlockedTypes) || tileNeighbor.HasAnyCover(Cover.Down))
                            && tileNeighbor.IsAnyType(TileType.MovableTypes))
                        {
                            // Below's neighbor in this dir is blocked or this neighbor has a 'floor', can move there.
                            tile.AddEdges(Tile.DirectionToEdge(dir));
                        }
                    }
                }
            }
            else
            {
                tile = AddLateralEdgesTowardNeighbors(in tiles, tile, data);
            }

            return tile;
        }

        /// <summary>
        /// Linecasts from tile to all direct cubic directions on Data.coverLayer. Should only be called after
        /// TileTypes have been assigned.
        /// </summary>
        /// <param name="tile">The source tile</param>
        /// <returns>The Edges that have a cover object between the tiles</returns>
        private static Cover LinecastCover(in Tile[] tiles, Tile tile, GridData data)
        {
            var covers = Cover.None;
            var directions = Tile.Directions_Cubic;

            foreach (var dir in directions)
            {
                if (tile.IsAtLimit(dir, data.size)) { continue; }

                covers |= LinecastCoverSingle(in tiles, tile, dir, data);
            }

            return covers;
        }

        private static Cover LinecastCoverSingle(in Tile[] tiles, Tile tile, Tile dir, GridData data)
        {
            Cover cover = Cover.None;

            if (Grid.GetIndex(tile + dir, data, out int index) && tiles[index].IsAnyType(TileType.MovableTypes))
            {
                var offset = Vector3.up * data.cellSize.y * 0.5f;   // Vector3 from the tile "origin" to the middle.
                var from = Grid.TileToWorld(tile, data) + offset;
                var to = Grid.TileToWorld(tile + dir, data) + offset;

                if (Physics.Linecast(from, to, data.coverLayer))
                {
                    cover = (Cover)Tile.DirectionToEdge(dir);
                }
            }

            return cover;
        }

        /// <summary>
        /// Uses Physics.CheckBox to detect anything on given layers in the node's world position. 
        /// NOTE: CAN'T BE USED INSIDE JOBS B/C REGULAR PHYSICS CASTS FAIL THERE!
        /// </summary>
        private static TileType BoxcastTileType(Tile tile, LayerMask layers, GridData data, bool includeTriggers = false)
        {
            var pos = Grid.TileToWorld(tile, data) + Vector3.up * data.cellSize.y * 0.5f;
            var radius = data.cellSize * data.obstacleCastRadius;
            var interaction = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var colliders = Physics.OverlapBox(pos, radius, Quaternion.identity, layers, interaction);

            if (colliders.Length > 1)
            {
                Debug.LogWarning($"BoxcastTileType found {colliders.Length} colliders in {tile}."
                    + " Check for multiple objects, or try reducing Obstacle Cast Radius");
                Debug.DrawLine(Grid.TileToWorld(tile, data), Grid.TileToWorld(tile + Tile.up * data.size.y, data), Color.red, 10f);
            }

            if (colliders.Length > 0) { return GridData.LayerMapping(colliders[0].gameObject.layer, data); }

            return TileType.Empty;
        }

        #endregion

        #region Custom inspector methods

        public void UpdateTile(Tile t, bool updateAsset)
        {
            if (updateAsset) 
            {
                if (asset.UpdateTile(t)) { Debug.Log($"Updated Tile {t} in Asset {asset.name}. "); }
                else { Debug.LogError($"Could not update tile {t} - Not found in Asset {asset.name}"); }                              
            }

            if (GridMap.UpdateTile(t)) { Debug.Log($"Updated Grid with Tile {t}"); }
            else  { Debug.LogError($"Could not update tile {t} in runtime graph - Tile not found"); }
        }

        [ContextMenu("Auto setup from unstored data", false, 0)]
        public void CreateFromData()
        {
            GridMap = new Grid(Data, true);
            GridMap.SetTiles(AutoBuild(GridMap.Tiles, Data));     
        }

        [ContextMenu("Auto setup from existing data", false, 1)]
        public void RebuildExisting()
        {
            GridMap = new Grid(GridMap.Data, true);
            GridMap.SetTiles(AutoBuild(GridMap.Tiles, Data));
        }

        [ContextMenu("Save graph to asset", false, 2)]
        public void Save()
        {
            asset.SaveToAsset(GridMap.Tiles, GridMap.Data);
        }

        [ContextMenu("Force Load graph from asset", false, 3)]
        public void ForceLoad()
        {           
            LoadGraph(log: true);
            Data = GridMap.Data;
        }

        public void TryLoad()
        {
            if (GridMap.IsInitialized) { return; }

            LoadGraph(log: true);
            Data = GridMap.Data;
        }
        #endregion
    }
}