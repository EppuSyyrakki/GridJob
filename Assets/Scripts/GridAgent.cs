using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GridJob
{
    public class GridAgent : MonoBehaviour
    {
        public Tile Current { get; private set; }
        public List<Tile> Path { get; private set; }



        private void OnPathComplete(in List<Tile> path, bool isPartial)
        {
            Path = new List<Tile>(path);
        }

        public void RequestPath(Tile target)
        {
            var request = new GridJobItem(Current, target, OnPathComplete, gameObject.name);
            GridSystem.PathQueue.Enqueue(request);
        }

        public void RequestField(Tile center)
        {

        }
    }
}