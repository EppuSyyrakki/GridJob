using System.Collections.Generic;

namespace GridSystem
{
    public enum JobType { None, Field, Path, Fov }

    public struct GridJobItem
    {
        public Tile Start { get; }
        public Tile Target { get; }
        public JobType Type { get; }
        public int FieldRange { get; }
        public int DropDepth { get; }
        public bool IncludeStart { get; }
        public int AgentId { get; }

        public delegate void JobCompleteDelegate(in Tile[] path);
        public event JobCompleteDelegate JobComplete;

        /// <summary>
        /// Creates a new A* pathfinding job returning a path from start to target.
        /// </summary>
        public GridJobItem(Tile start, Tile target, int dropDepth, JobCompleteDelegate onComplete, int id, bool includeStart = false)
        {
            Start = start;
            Target = target;
            FieldRange = -1;           
            Type = JobType.Path;
            JobComplete = onComplete;
            AgentId = id;
            IncludeStart = includeStart;
            DropDepth = dropDepth;
        }

        /// <summary>
        /// Creates a new Dijkstra pathfinding job returning all tiles within fieldRange from start.
        /// </summary>
        public GridJobItem(Tile center, int fieldRange, int dropDepth, JobCompleteDelegate onComplete, int id, bool includeStart = false)
        {
            Start = center;
            Target = Tile.MaxValue;
            FieldRange = fieldRange;            
            Type = JobType.Field;
            JobComplete = onComplete;
            AgentId = id;
            IncludeStart = includeStart;
            DropDepth = dropDepth;
        }

        public void Complete(in Tile[] tiles)
        {
            JobComplete?.Invoke(in tiles);
        }
    }
}