﻿using System;

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
        AllSameLevel = 1 << 0 | 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 5 | 1 << 6 | 1 << 7,
        All = 1 << 0 | 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 5 | 1 << 6 | 1 << 7 | 1 << 8 | 1 << 9
    }

    public enum TileType : byte
    {
        Empty = 0,
        Terrain = 1,
        Obstacle = 2,
        Climbable = 3
    }
}