namespace GridSystem
{
    public static class Extensions
	{
		public static bool IsAnyOf(this Edge e, Edge edges)
        {
			return (e & edges) > 0;
        }

        public static (Edge e1, Edge e2) Adjacents(this Edge e)
        {
            if (e.Equals(Edge.North)) { return (Edge.NorthEast, Edge.NorthWest); }
            if (e.Equals(Edge.NorthEast)) { return (Edge.North, Edge.East); }
            if (e.Equals(Edge.East)) { return (Edge.NorthEast, Edge.SouthEast); }
            if (e.Equals(Edge.SouthEast)) { return (Edge.South, Edge.East); }
            if (e.Equals(Edge.South)) { return (Edge.SouthEast, Edge.SouthWest); }
            if (e.Equals(Edge.SouthWest)) { return (Edge.South, Edge.West); }
            if (e.Equals(Edge.West)) { return (Edge.SouthWest, Edge.NorthWest); }
            if (e.Equals(Edge.NorthWest)) { return (Edge.North, Edge.West); }
            return (Edge.None, Edge.None);
        }

        public static Edge Opposite(this Edge e)
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

        public static Cover Opposite(this Cover e)
        {
            if (e.Equals(Cover.North)) { return Cover.South; }
            if (e.Equals(Cover.East)) { return Cover.West; }
            if (e.Equals(Cover.South)) { return Cover.North; }
            if (e.Equals(Cover.West)) { return Cover.East; }
            if (e.Equals(Cover.Up)) { return Cover.Down; }
            if (e.Equals(Cover.Down)) { return Cover.Up; }
            return Cover.None;
        }
    }
}