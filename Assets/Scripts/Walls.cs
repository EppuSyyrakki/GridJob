
using System;
using UnityEngine;

namespace GridSystem
{
    [Serializable]
    public struct Walls
    {
        [SerializeField]    // Kind of hacky, but hey - it's safe and jobs compatible
        private WallType north, east, south, west, up, down;

        [SerializeField]
        public int strength;

        public WallType North => north;
        public WallType East => east;
        public WallType South => south;
        public WallType West => west;
        public WallType Up => up;
        public WallType Down => down;

        public WallMask GetMask(WallTypeMask type)
        {
            var mask = WallMask.None;

            for (int i = 0; i < 6; i++)
            {
                WallTypeMask current = (WallTypeMask)GetByIndex(i);
                mask = (current & type) > 0 ? mask | (WallMask)(1 << i) : mask;
            }

            return mask;
        }

        public void SetMask(WallMask mask, WallType type)
        {
            for (int i = 0; i < 6; i++)
            {
                if ((mask & (WallMask)(1 << i)) > 0)
                {
                    SetByIndex(i, type);
                }
            }
        }

        public readonly WallType GetByIndex(int i)
        {
            if (i == 0) return north;
            if (i == 1) return east;
            if (i == 2) return south;
            if (i == 3) return west;
            if (i == 4) return up;
            if (i == 5) return down;

            return WallType.None;
        }

        public void SetByIndex(int i, WallType type)
        {
            if (i == 0) north = type;
            if (i == 1) east = type;
            if (i == 2) south = type;
            if (i == 3) west = type;
            if (i == 4) up = type;
            if (i == 5) down = type;
        }

        public readonly WallType GetWall(Wall wall)
        {
            if (wall == Wall.North) return north;
            if (wall == Wall.East) return east;
            if (wall == Wall.South) return south;
            if (wall == Wall.West) return west;
            if (wall == Wall.Up) return up;
            if (wall == Wall.Down) return down;
            return WallType.None;
        }

        public void SetWall(Wall wall, WallType type)
        {
            if (wall == Wall.North) north = type;
            if (wall == Wall.East) east = type;
            if (wall == Wall.South) south = type;
            if (wall == Wall.West) west = type;
            if (wall == Wall.Up) up = type;
            if (wall == Wall.Down) down = type;
        }
    }
}