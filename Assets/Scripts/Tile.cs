using System;
using UnityEngine;
using Unity.Mathematics;

namespace GridJob
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
        public Edge covers;
        [SerializeField]
        public int index;

        #region Properties
        public TileType Type => types;
        public Edge Edges => edges;
        public Edge Covers => covers;
        #endregion

        #region Constructors
        public Tile(int x, int y, int z, Edge edges = (Edge)ushort.MaxValue, Edge covers = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3(x, y, z);
            types = type;
            this.edges = edges;
            this.covers = covers;
            this.index = index;            
        }

        public Tile(Vector3 v, Edge edges = (Edge)ushort.MaxValue, Edge covers = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3((sbyte)math.round(v.x), (sbyte)math.round(v.y), (sbyte)math.round(v.z));
            types = type;
            this.edges = edges;
            this.covers = covers;
            this.index = index;           
        }

        public Tile(float x, float y, float z, Edge edges = (Edge)ushort.MaxValue, Edge covers = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3((sbyte)math.round(x), (sbyte)math.round(y), (sbyte)math.round(z));
            types = type;
            this.edges = edges;
            this.covers = covers;
            this.index = index;
        }

        public Tile(sbyte x, sbyte y, sbyte z, Edge edges = (Edge)ushort.MaxValue, Edge covers = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            data = new sbyte3(x, y, z);
            types = type;
            this.edges = edges;
            this.covers = covers;
            this.index = index;
        }

        public Tile(sbyte3 data, Edge edges = (Edge)ushort.MaxValue, Edge covers = (Edge)ushort.MaxValue, TileType type = 0, int index = -1)
        {
            this.data = data;
            types = type;
            this.edges = edges;
            this.covers = covers;
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
        public void SetCovers(Edge c) { covers = c; }
        public void AddEdges(Edge e) { edges |= e; }
        public void AddCovers(Edge c) { covers |= c; }
        public void RemoveEdges(Edge e) { edges &= ~e; }
        public void RemoveCovers(Edge c) { covers &= ~c; }
        public void ToggleEdges(Edge e) { edges ^= e; }    
        public void ToggleCovers(Edge c) { covers ^= c; }
        public bool HasAnyEdge(Edge e) { return (edges & covers & e) > 0; }
        public bool HasNoEdge(Edge e) { return (edges & covers & e) == 0 ; }
        public bool HasEdgeTo(Tile other)
        {
            Tile dir = this - other;
            sbyte3 abs = dir.data.Abs;

            if (abs.x > 1 || abs.y > 1 || abs.z > 1) { return false; }  // other is non-adjacent 

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

        public Tile Normalized() { return new Tile(data.Normalized, edges, covers, types, index); }       
        public float Magnitude() 
        {
            var d = data.Abs;
            return math.sqrt(math.pow(d.x, 2) + math.pow(d.y, 2) + math.pow(d.z, 2));
        }
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
        public static Edge DirectionToEdge(Tile tile)
        {
            Tile n = tile.Normalized();
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
        public static Tile MaxValue => new Tile(sbyte.MaxValue, sbyte.MaxValue, sbyte.MaxValue);
        #endregion

        #region Overrides and Interfaces
        /// <summary> NOTE: Copies node data from Node a. </summary>
        public static Tile operator +(Tile a, Tile b)
        {
            a.data = new sbyte3(a.data.x + b.data.x, a.data.y + b.data.y, a.data.z + b.data.z);
            return a;
        }

        /// <summary> NOTE: Copies node data from Node b </summary>
        public static Tile operator -(Tile a, Tile b)
        {
            b.data = new sbyte3(a.data.x - b.data.x, a.data.y - b.data.y, a.data.z - b.data.z);
            return b;
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

    /// <summary>
    /// Helper struct to be able to use Unity.Mathematics lerp functions when getting lines of tiles.
    /// </summary>
    //public struct FractionTile
    //{
    //    public float3 data;

    //    public FractionTile(float3 data) { this.data = data; }

    //    public FractionTile(float x, float y, float z) { data = new float3(x, y, z); }

    //    public FractionTile(Vector3 v) { data = new float3(v); }

    //    public static implicit operator Tile(FractionTile ft)
    //    { 
    //        return new Tile((int)ft.data.x, (int)ft.data.y, (int)ft.data.z);
    //    }
    //}
}
