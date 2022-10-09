using System;

namespace GridJob
{
    [Flags]
    public enum Edge : ushort
    {      
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
        Up = 1 << 4,
        Down = 1 << 5,
        NorthEast = 1 << 6,
        SouthEast = 1 << 7,
        SouthWest = 1 << 8,
        NorthWest = 1 << 9,
        All = North | East | South | West | Up | Down | NorthEast | SouthEast | SouthWest | NorthWest,
        AllSameLevel = AllDirect | AllDiagonal,       
        AllDirect = North | East | South | West,
        AllDiagonal = NorthEast | SouthEast | SouthWest | NorthWest
    }

    [Flags]
    public enum Cover : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
        Up = 1 << 4,
        Down = 1 << 5
    }

    [Flags]
    public enum TileType : byte
    {
        None = 0,
        All = Empty | Terrain | Cover | Structure | Climb | Jump | Occupied,
        MovableTypes = Climb | Empty | Jump,
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