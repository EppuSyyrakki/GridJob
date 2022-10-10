using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GridSystem
{
    public class GridAgent : MonoBehaviour
    {
        [SerializeField]    // debug value
        private int range = 8;

        public Tile Current { get; private set; }
        public List<Tile> Path { get; private set; }
        public List<Tile> Field { get; private set; }

        private void OnPathComplete(in Tile[] path)
        {
            Path = new List<Tile>(path);
        }

        private void OnFieldComplete(in Tile[] field)
        {
            Field = new List<Tile>(field);
        }

        public void RequestPath(Tile target)
        {
            MasterGrid.Request(new GridJobItem(Current, target, OnPathComplete, GetInstanceID()));
        }

        public void RequestField()
        {
            MasterGrid.Request(new GridJobItem(Current, range, OnFieldComplete, GetInstanceID()));
        }
    }
}