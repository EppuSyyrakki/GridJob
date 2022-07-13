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
        public int3 data;
        [SerializeField]
        private TileType types;
        [SerializeField]
        public Edge edges;
        [SerializeField]
        public bool occupied;
        [SerializeField]
        public int index;

        #region Properties
        public TileType Type => types;
        public Edge Edges => edges;
        public static Tile MaxValue { get { return new Tile(int.MaxValue, int.MaxValue, int.MaxValue); } }
        #endregion

        #region Constructors
        public Tile(int x, int y, int z, Edge edges = (Edge)ushort.MaxValue, TileType type = 0, bool occupied = false, int index = -1)
        {
            data = new int3(x, y, z);
            this.types = type;
            this.edges = edges;
            this.occupied = occupied;
            this.index = index;
        }

        public Tile(int3 data, Edge edges = (Edge)ushort.MaxValue, TileType type = 0, bool occupied = false, int index = -1)
        {
            this.data = data;
            this.types = type;
            this.edges = edges;
            this.occupied = occupied;
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
            var abs = math.abs(dir.data);

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

        public Tile Normalized() { return new Tile(math.clamp(data, -one.data, one.data), Edges, types, occupied, index); }
        public float Magnitude_Float() { return math.abs(data.x * math.SQRT2 + data.y * math.SQRT2 + data.z * math.SQRT2); }
        public int Magnitude_Int() { return (int)Magnitude_Float(); }

        public static int3 Abs(Tile n) { return new int3(math.abs(n.data.x), math.abs(n.data.y), math.abs(n.data.z)); }
        public static Tile Lerp(Tile start, Tile end, float t) { return start + (end - start) * t; }
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
        public static Edge OppositeEdge(Edge e)
        {
            if (e.Equals(Edge.North)) { return Edge.South; }
            if (e.Equals(Edge.NorthEast)) { return Edge.SouthWest; }
            if (e.Equals(Edge.East)) { return Edge.West; }
            if (e.Equals(Edge.SouthEast)) { return Edge.NorthWest; }
            if (e.Equals(Edge.South)) { return Edge.North; }
            if (e.Equals(Edge.SouthWest)) { return Edge.NorthEast; }
            if (e.Equals(Edge.West)) { return Edge.East; }
            if (e.Equals(Edge.NorthWest)) { return Edge.SouthEast; }
            if (e.Equals(Edge.Up)) { return Edge.Down; }
            if (e.Equals(Edge.Down)) { return Edge.Up; }
            return Edge.None;
        }
        #endregion

        #region Static properties
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
            a.data = new int3(a.data.x + b.data.x, a.data.y + b.data.y, a.data.z + b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node data from Node a. </summary>
        public static Tile operator -(Tile a, Tile b)
        {
            a.data = new int3(a.data.x - b.data.x, a.data.y - b.data.y, a.data.z - b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator *(Tile a, int k)
        {
            a.data = new int3(a.data.x * k, a.data.y * k, a.data.z * k);
            return a;
        }

        /// <summary> NOTE: Copies node a data to new node. </summary>
        public static Tile operator *(Tile a, float f)
        {        
            float3 f3 = new float3(a.data.x * f, a.data.y * f, a.data.z * f);
            a.data = new int3(f3);
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
