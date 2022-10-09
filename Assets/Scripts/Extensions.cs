using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using System;

namespace GridJob
{
    public static class Extensions
	{
		public static bool IsAnyOf(this Edge e, Edge edges)
        {
			return (e & edges) > 0;
        }

        public static Cover ToCover(this Edge e)
        {
            var result = Cover.None;

            for (int i = 0; i < Enum.GetValues(typeof(Cover)).Length - 1; i++)  // -1 because None doesn't count
            {
                if (e.IsAnyOf((Edge)(1 << i))) { result |= (Cover)(1 << i); }
            }

            return result;
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
    }
}