using System.Collections;
using UnityEngine;

namespace Jobben
{
    public class PathRequest
    {
        public Tile Start { get; private set; }
        public Tile Goal { get; private set; }
        public Tile[] Path { get; private set; }

        public bool isPartial;
        public int id;

        public PathRequest(Tile start, Tile goal)
        {
            Start = start;
            Goal = goal;
        }
    }
}