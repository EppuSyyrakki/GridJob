using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Jobben
{
	public static class Extensions
	{
		public static void AddOrModify<T1, T2>(this Dictionary<T1, T2> dict, T1 key, T2 value)
		{
			if (dict.ContainsKey(key)) { dict[key] = value; return; }
			dict.Add(key, value);
		}

		public static int3 ToInt3(this Vector3Int v)
        {
			return new int3(v.x, v.y, v.z);
        }

		public static Vector3 ToVector3(this int3 i)
        {
			return new Vector3(i.x, i.y, i.z);
        }

		public static int3 MaxValue(this int3 _)
        {
			return new int3(int.MaxValue, int.MaxValue, int.MaxValue);
        }

		public static bool IsAnyOf(this Edge e, Edge edges)
        {
			return (e & edges) > 0;
        }
	}
}