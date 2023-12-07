using System.Collections.Generic;
using Unity.Mathematics;

namespace GridSystem
{
    public struct Heuristic : IComparer<Tile>
    {
        private readonly GridData data;
        private Tile target;

        public Tile Target { set { target = value; } }

        /// <summary>
        /// Creates a comparison struct that is used in PathJob frontier to organize the frontier/open list by their 
        /// manhattan distance to the target node.
        /// </summary>
        public Heuristic(Tile target, GridData data)
        {
            this.target = target;
            this.data = data;
        }

        public int Compare(Tile a, Tile b)
        {
            int distFromA = Manhattan(a, target, data);
            int distFromB = Manhattan(b, target, data);
            if (distFromA < distFromB) { return -1; }
            else if (distFromA > distFromB) { return 1; }
            return 0;
        }

        /// <summary> Calculates a height-modded manhattan distance between 2 tiles. </summary>
        public static int Manhattan(Tile a, Tile b, GridData data)
        {
            sbyte3 dist = a.data - b.data;
            int height = dist.y > 0 ? data.upCost : (int)(data.directCost * 0.5f);
            return math.abs(data.diagonalCost * math.min(dist.x, dist.z))
                + math.abs(data.directCost * math.max(dist.x, dist.z))
                + math.abs(dist.y * height);
        }
    }
}