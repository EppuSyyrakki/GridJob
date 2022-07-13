using System;

namespace Jobben
{
    [Flags]
    public enum Edge : ushort
    {      
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
        NorthEast = 1 << 4,
        SouthEast = 1 << 5,
        SouthWest = 1 << 6,
        NorthWest = 1 << 7,
        Up = 1 << 8,
        Down = 1 << 9,
        AllSameLevel = AllDirect | AllDiagonal,
        All = North | East | South | West | NorthEast | SouthEast | SouthWest | NorthWest | Up | Down,
        AllDirect = North | East | South | West,
        AllDiagonal = NorthEast | SouthEast | SouthWest | NorthWest
    }

    [Flags]
    public enum TileType : byte
    {
        None = 0,        
        WalkableTypes = Climb | Empty | Jump,
        BlockedTypes = Terrain | Cover | Structure | Occupied,
        Empty = 1 << 0,
        Terrain = 1 << 1,   // Has no edges to any direction (these should be true from Graph.AutoBuild())
        Cover = 1 << 2,     // Has no edges to any direction
        Structure = 1 << 3, // Has no edges to any direction
        Climb = 1 << 4,     // Has at least an up edge
        Jump = 1 << 5,      // Has only a down edge
        Occupied = 1 << 6   // Has any edges, but is otherwise unwalkable (ie. a character is in it)
    }
}