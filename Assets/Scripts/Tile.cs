using System;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Jobben
{
    [Serializable]
    public struct Tile : IEquatable<Tile>
    {
        [SerializeField]
        public sbyte3 data;
        [SerializeField]
        private TileType types;
        [SerializeField]
        public Edge edges;
        [SerializeField]
        public int index;

        #region Properties
        public TileType Type => types;
        public Edge Edges => edges;
        public static Tile MaxValue { get { return new Tile(sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue); } }
        #endregion

        #region Constructors
        public Tile(int x, int y, int z, Edge edges = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3(x, y, z);
            types = type;
            this.edges = edges;
            this.index = index;
        }

        public Tile(sbyte x, sbyte y, sbyte z, Edge edges = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3(x, y, z);
            types = type;
            this.edges = edges;
            this.index = index;
        }

        public Tile(sbyte3 data, Edge edges = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            this.data = data;
            types = type;
            this.edges = edges;
            this.index = index;
        }
        #endregion

        #region Tile, Type and Edge operations        
        public void SetType(TileType t) { types = t; }
        public void AddType(TileType t) { types |= t; }
        public void RemoveType(TileType t) { types &= ~t; }
        /// <summary> Returns true if tile has any same flags set as param types. </summary>
        public bool IsAnyType(TileType types) { return (types & this.types) > 0; }
        public void SetEdges(Edge e) { edges = e; }
        public void AddEdges(Edge e) { edges |= e; }
        public void RemoveEdges(Edge e) { edges &= ~e; }
        public void ToggleEdges(Edge e) { edges ^= e; }        
        public bool HasAnyEdge(Edge e) { return (edges & e) > 0; }
        public bool HasEdgeTo(Tile other)
        {
            Tile dir = (this - other);
            sbyte3 abs = dir.data.Abs;

            if (abs.x > 1 || abs.y > 1 || abs.z > 1) { Debug.LogWarning(this + " is checking edges to non-adjacent node."); }

            for (int i = 0; i < Directions_All.Length; i++) // i == 4 is at northeast
            {
                Edge current = (Edge)(1 << i);  // edge 1 << 4 is northeast

                if (HasAnyEdge(current) && dir.Equals(Directions_All[i] * -1)) // opposite 4 is sw
                {
                    return true;
                }
            }

            return false;
        }

        public Tile Normalized() { return new Tile(data.Normalized, Edges, types, index); }       
        public float Magnitude() { return math.abs(data.x * math.SQRT2 + data.y * math.SQRT2 + data.z * math.SQRT2); }
        public static Tile EdgeToDirection(Edge e)
        {
            var directions = Directions_All;

            for (int i = 0; i < directions.Length; i++)
            {
                Edge current = (Edge)(1 << i);
                if ((e & current) == 0) { return directions[i]; }
            }

            return zero;
        }
        public static Edge DirectionToEdge(Tile node)
        {
            Tile n = node.Normalized();

            var directions = Directions_All;

            for (int i = 0; i < directions.Length; i++)
            {
                Edge current = (Edge)(1 << i);
                if (n.Equals(directions[i])) { return current; }
            }

            return Edge.None;
        }
        #endregion

        #region Static directions
        public static Tile[] Directions_All => new Tile[] { n, e, s, w, ne, se, sw, nw, up, down };
        public static Tile[] Directions_Direct => new Tile[] { n, e, s, w };
        public static Tile[] Directions_Diagonal => new Tile[] { ne, se, sw, nw };
        public static Tile[] Directions_Lateral => new Tile[] { n, e, s, w, ne, se, sw, nw };
        public static Tile zero => new Tile(0, 0, 0);
        public static Tile one => new Tile(1, 1, 1);
        public static Tile n => new Tile(0, 0, 1);
        public static Tile ne => new Tile(1, 0, 1);
        public static Tile e => new Tile(1, 0, 0);
        public static Tile se => new Tile(1, 0, -1);
        public static Tile s => new Tile(0, 0, -1);
        public static Tile sw => new Tile(-1, 0, -1);
        public static Tile w => new Tile(-1, 0, 0);
        public static Tile nw => new Tile(-1, 0, 1);
        public static Tile up => new Tile(0, 1, 0);
        public static Tile down => new Tile(0, -1, 0);
        #endregion

        #region Overrides and Interfaces
        /// <summary> NOTE: Copies node data from Node a. </summary>
        public static Tile operator +(Tile a, Tile b)
        {
            a.data = new sbyte3(a.data.x + b.data.x, a.data.y + b.data.y, a.data.z + b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node data from Node a. </summary>
        public static Tile operator -(Tile a, Tile b)
        {
            a.data = new sbyte3(a.data.x - b.data.x, a.data.y - b.data.y, a.data.z - b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator *(Tile a, int k)
        {
            a.data = new sbyte3(a.data.x * k, a.data.y * k, a.data.z * k);
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator +(Tile a, Edge e)
        {
            return a + EdgeToDirection(e);
        }

        public override int GetHashCode() 
        {
            return data.GetHashCode();
        }

        public override string ToString()
        {
            return $"({data.x}, {data.y}, {data.z})";
        }

        public bool Equals(Tile other)
        {
            return data.x == other.data.x && data.y == other.data.y && data.z == other.data.z;
        }
        #endregion
    }
}
