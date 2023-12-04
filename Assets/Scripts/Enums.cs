using System;

namespace GridSystem
{
    public enum Wall : byte
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
    public enum WallMask : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
        Up = 1 << 4,
        Down = 1 << 5,
        NorthEast = North | East,
        NorthWest = North | West,
        SouthWest = South | West,
        SouthEast = South | East,
        All = North | South | East | West | Up | Down
    }

    public enum WallType : byte
    {
        None = 0,
        Movable = 1 << 0,
        Partial = 1 << 1,
        Full = 1 << 2,
        Climbable = 1 << 3
    }

    [Flags]
    public enum WallTypeMask : byte
    {
        None = 0,
        Movable = 1 << 0,
        Partial = 1 << 1,
        Full = 1 << 2,
        Climbable = 1 << 3,
        All = Movable | Partial | Full | Climbable,
        AllBlocked = Partial | Climbable | Full
    }
}