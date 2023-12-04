namespace GridSystem
{
    public static class Extensions
	{
        public static Wall Opposite(this Wall w)
        {
            if (w == Wall.North) return Wall.South;
            if (w == Wall.East) return Wall.West;
            if (w == Wall.South) return Wall.North;
            if (w == Wall.West) return Wall.East;
            if (w == Wall.Up) return Wall.Down;
            if (w == Wall.Down) return Wall.Up;

            return Wall.None;
        }
    }
}