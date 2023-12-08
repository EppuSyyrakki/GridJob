using GridSystem.Jobs;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GridSystem
{
    public class GridEditor : MonoBehaviour
    {
        [System.Flags]
        public enum Helpers : byte
        { 
            None = 0, 
            LogPathfinder = 1 << 0, 
            DrawPathfinder = 1 << 1, 
            DrawResult = 1 << 2, 
            DrawAllTiles = 1 << 3, 
            DrawSelectedTile = 1 << 4 
        }

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
        private Tile start = Tile.Zero;
        private JobHandle pathHandle;
        private JobHandle fieldHandle;
        private JobHandle fovHandle;
        private NativeList<Tile> n_pathResult;  // Allocator.TempJob
        private NativeList<Tile> n_fieldResult;   // Allocator.TempJob
        private NativeHashSet<Tile> n_fovResult;   // Allocator.TempJob
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

            if (!GridMap.IsInitialized) { ForceLoad(); }
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

            if (Input.GetMouseButton(0))
            {
                Tile target = MouseCastTile();

                if (target.Equals(Tile.MaxValue)) { return; }

                if (jobType == JobType.Field && ScheduleFieldJob(target, fieldRange))
                {
                    scheduled = JobType.Field;
                }
                else if (jobType == JobType.Path && SchedulePathJob(start, target))
                {
                    scheduled = JobType.Path;
                }
                else if (jobType == JobType.Fov && ScheduleFovJob(start, target - start, 90f))
                {
                    scheduled = JobType.Fov;
                }
            }
        }

        /// <summary> Debugging method to test the Fov mechanic </summary>
        private IEnumerator DrawFov(Tile center, Tile forward, float angle)
        {
            fov.Clear();
            Tile right = forward.Rotate(-angle * 0.5f);
            Tile current = right;
            var line = new List<Tile>(GridMap.Linecast(center, center + current));
            yield return AddToResult(line);

            for (float rotation = 1; rotation < angle; rotation++)
            {
                Tile next = right.Rotate(rotation);

                if (math.cos(forward.Magnitude) < math.min(Data.cellSize.x, Data.cellSize.z)) 
                {
                    Debug.DrawLine(Grid.TileToWorld(center, Data), Grid.TileToWorld(center + next, Data), Color.magenta, 20f);
                    continue;  // rotation wasn't enough to get a new end tile
                }

                line.Clear();
                line.AddRange(GridMap.Linecast(center, center + next));
                yield return AddToResult(line);
                current = next;
            }

            IEnumerator AddToResult(List<Tile> line)
            {
                for (int i = line.Count - 1; i >= 0; i--)
                {
                    if (!fov.Contains(line[i]))
                    {
                        fov.Add(line[i]);
                        DrawSingle(line[i], Color.green);
                        yield return new WaitForSeconds(0.1f);
                    }
                    else
                    {
                        DrawSingle(line[i], Color.red);
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }

            void DrawSingle(Tile t, Color c)
            {
                Debug.DrawRay(Grid.TileToWorld(t, Data) + Vector3.one * UnityEngine.Random.Range(0.01f, 0.1f), Vector3.up, c, 20f);
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
                foreach (Tile t in GridMap.Tiles) { WireCube(t, t.walls.GetMask(WallTypeMask.Full) == WallMask.All ? Color.black : gridColor); }
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
            if (tiles == null || tiles.Count == 0) { return; }

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

            if (log) { Debug.Log($"Scheduling Path Job: From {start} To {goal}. Heuristic: {mDist}"); }

            if (!Grid.HasTile(start, GridMap.Data) || !Grid.HasTile(goal, GridMap.Data)) { return false; }

            n_pathResult = new NativeList<Tile>(GridMap.Data.maxPathLength, Allocator.TempJob);
            this.start = start;           
            var job = new AStarPathJob(start, goal, n_tiles, n_pathResult, GridMap.Data, dropDepth);
            pathHandle = job.Schedule();
            return true;
        }

        private bool ScheduleFieldJob(Tile center, int fieldRange, int dropDepth = 1, int jumpHeight = 0)
        {
            bool log = (helpers & Helpers.LogPathfinder) > 0;

            if (log) { Debug.Log($"Scheduling distance field job: From {center}, Dist: {fieldRange}"); }

            if (!Grid.HasTile(center, GridMap.Data) 
                || fieldRange > Mathf.Max(GridMap.Data.size.x, GridMap.Data.size.z)) { return false; }

            n_fieldResult = new NativeList<Tile>(fieldRange * fieldRange, Allocator.TempJob);
            start = center;
            var job = new DijkstraFieldJob(center, fieldRange, n_tiles, n_fieldResult, GridMap.Data, dropDepth, jumpHeight, false);
            fieldHandle = job.Schedule();

            return true;
        }

        private bool ScheduleFovJob(Tile center, Tile forward, float angleWidth)
        {
            if (!Grid.HasTile(center, GridMap.Data)) { return false; }

            int cap = (int)forward.Magnitude * (int)(angleWidth / 2);
            n_fovResult = new NativeHashSet<Tile>(cap, Allocator.TempJob);
            start = center;
            var job = new FovJob(n_tiles, GridMap.Data, n_fovResult, start, forward, angleWidth);
            fovHandle = job.Schedule();

            //ShadowCast caster = new ShadowCast(GridMap.Tiles, Data);
            //StartCoroutine(caster.Cast(center, forward.Magnitude));
            //fov = caster.Result;
            return true;
        }

        private void CompleteAndDispose(ref JobHandle handle, ref NativeList<Tile> from, List<Tile> to) 
        {
            if (!Input.GetKeyDown(KeyCode.LeftShift)) { to.Clear(); ; }
           
            handle.Complete();
            foreach(Tile t in from) { to.Add(t); }
            from.Dispose();
        }

        private void CompleteAndDispose(ref JobHandle handle, ref NativeHashSet<Tile> from, List<Tile> to)
        {
            if (!Input.GetKeyDown(KeyCode.LeftShift)) { to.Clear(); ; }

            handle.Complete();
            foreach (Tile t in from) { to.Add(t); }
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
            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = tiles[i];
                tile.walls = RaycastWalls(tile, data, true);
                tiles[i] = tile;
            }

            return tiles;
        }

        /// <summary>
        /// Uses Physics.CheckBox to detect anything on given layers in the node's world position. 
        /// NOTE: CAN'T BE USED INSIDE JOBS B/C REGULAR PHYSICS CASTS FAIL THERE!
        /// </summary>
        private static Walls RaycastWalls(Tile tile, GridData data, bool includeTriggers = false)
        {
            Vector3 pos = Grid.TileToWorld(tile, data);
            Vector3 mid = pos + 0.5f * data.cellSize.y * Vector3.up;
            var interaction = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            var directions = Tile.Directions_Cubic;
            Walls walls = new();

            if (Physics.CheckBox(mid, data.cellSize * 0.3f, Quaternion.identity, data.blockedLayers, interaction))
            {
                walls.SetMask(WallMask.All, WallType.Full);
            }
            else
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    var dir = directions[i];
                    float distance = dir.data.y == 0 ? data.cellSize.x : data.cellSize.y;
                    var hits = Physics.RaycastAll(mid, dir, distance, data.AllLayers, interaction);

                    for (int j = 0; j < hits.Length; j++)
                    {
                        if ((1 << hits[j].collider.gameObject.layer & data.blockedLayers) > 0)
                        {
                            Debug.DrawLine(mid, mid + dir * distance, Color.red, 5f);
                            walls.SetByIndex(i, WallType.Full);
                        }

                        if ((1 << hits[j].collider.gameObject.layer & data.climbLayers) > 0)
                        {
                            Debug.DrawLine(mid, mid + dir * distance, Color.yellow, 5f);
                            walls.SetByIndex(i, WallType.Climbable);
                            break;
                        }
                    }

                    // TODO: Set boundary tiles (next to end of map) to full wall
                    // TODO: Some way to enter "movable" walls like windows, big holes and low obstacles
                }
            }

            return walls;
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