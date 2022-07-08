﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Jobben
{
    public class GraphUpdater
    {
		private readonly MapData data;
        private readonly LayerMask layers;

        private NativeArray<RaycastCommand> n_commands;
		private NativeArray<RaycastHit> n_result;

        private Tile[] tiles;
        private JobHandle handle;

        public GraphUpdater(Tile[] tiles, MapData data, LayerMask layers)
        {
            n_commands = new NativeArray<RaycastCommand>(tiles.Length, Allocator.TempJob);
            n_result = new NativeArray<RaycastHit>(tiles.Length, Allocator.TempJob);
            this.data = data;
            this.layers = layers;
            this.tiles = tiles;
        }

        /// <summary>
        /// Raycasts downward from nodes where y == highest. Results stored in n_hits.
        /// </summary>
        public void Schedule()
        {
            for (int i = 0; i < tiles.Length; i++)
            {               
                if (tiles[i].data.y != data.size.y - 1) { continue; }

                Vector3 offset = Vector3.up * data.cellSize.y * 0.9f;
                Vector3 origin = Graph.TileToWorld(tiles[i], data) + offset;
                float dist = data.size.y * data.cellSize.y;
                n_commands[i] = new RaycastCommand(origin, Vector3.down, dist, layers);
            }

            handle = RaycastCommand.ScheduleBatch(n_commands, n_result, 16);
        }

        public Tile[] Run()
        {
            handle.Complete();
            Tile[] result = new Tile[tiles.Length];
            RaycastHit hit;

            for (int i = 0; i < tiles.Length; i++)
            {
                hit = n_result[i];

                if (hit.collider == null) { continue; }

                Tile tile = tiles[i];
                int hitLayer = hit.collider.gameObject.layer;

                if (((1 << hitLayer) | data.terrainLayer) > 1)
                {
                    tile.SetEdges(Edge.None);

                    if (Graph.HasTile(tile + Tile.up, data.size))
                    {
                        Tile above = tiles[Graph.CalculateIndex(tile + Tile.up, data.size)];
                        // TODO: Something?
                    }
                }

                result[i] = tile;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                hit = n_result[i];

                if (hit.collider == null) { continue; }

                Tile tile = tiles[i];
                int hitLayer = hit.collider.gameObject.layer;

                if (((1 << hitLayer) | data.terrainLayer) > 1) { tile.SetEdges(Edge.None); }

                result[i] = tile;
            }

            return result;
        }

        public void Dispose()
        {
            n_commands.Dispose();
            n_result.Dispose();
        }

        //private bool RaycastNode(Node n, MapData data, LayerMask layers, bool debug = false)
        //{
        //	var pos = Graph.NodeToWorld(n, data) + Vector3.up * data.cellSize.y * 0.5f;
        //	var halfExtents = data.cellSize * data.obstacleCastRadius;

        //	if (Physics.CheckBox(pos, halfExtents, Quaternion.identity, layers))
        //	{
        //		if (debug) { Debug.DrawLine(pos, pos + Vector3.up, Color.red, 10f); }

        //		return true;
        //	}

        //	if (debug) { Debug.DrawLine(pos, pos + Vector3.up, Color.green, 10f); }
        //	return false;
        //}
    }
}