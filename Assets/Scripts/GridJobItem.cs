using System.Collections.Generic;
using Unity.Jobs;

namespace GridJob
{
    public enum JobType { None, Field, Path, Fov }

    public class GridJobItem
    {
        public Tile Start { get; }
        public Tile Target { get; }
        public JobType Type { get; }
        public int FieldRange { get; }
        public bool IncludeStart { get; }
        public string Agent { get; }

        public delegate void JobCompleteDelegate(in List<Tile> path, bool isPartial);
        public JobCompleteDelegate JobComplete;

        /// <summary>
        /// Creates a new A* pathfinding job returning a path from start to target.
        /// </summary>
        public GridJobItem(Tile start, Tile target, JobCompleteDelegate onComplete, string agent, bool includeStart = false)
        {
            Start = start;
            Target = target;
            FieldRange = -1;           
            Type = JobType.Path;
            JobComplete = onComplete;
            Agent = agent;
            IncludeStart = includeStart;
        }

        /// <summary>
        /// Creates a new Dijkstra pathfinding job returning all tiles within fieldRange from start.
        /// </summary>
        public GridJobItem(Tile start, int fieldRange, JobCompleteDelegate onComplete, string agent, bool includeStart = false)
        {
            Start = start;
            Target = Tile.MaxValue;
            FieldRange = fieldRange;            
            Type = JobType.Field;
            JobComplete = onComplete;
            Agent = agent;
            IncludeStart = includeStart;
        }

        public void Complete(in List<Tile> path)
        {
            bool isPartial = Type == JobType.Path && path[path.Count - 1].Equals(Target);
            JobComplete?.Invoke(in path, isPartial);
        }        
    }
}