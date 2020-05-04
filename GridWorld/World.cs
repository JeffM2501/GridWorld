#region copyright
/*
GridWorld a learning experiement in voxel technology
Copyright (c) 2020 Jeffery Myersn

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Urho;

namespace GridWorld
{
    /// <summary>
    /// runtime data for the active world. Stores all block defs, block indexes, and active clusters.
    /// clusters may be loaded and unloaded by managers as needed
    /// </summary>
    public static class World
    {
        public delegate void IntEventCallback(int index);
        public static event IntEventCallback BlockDefAdded = null;
        public static event IntEventCallback BlockIndexAdded = null;
        public static event IntEventCallback TextureAdded = null;

        public delegate void ClusterPosEventCallback(ClusterPos pos);
        public static event ClusterPosEventCallback ClusterAdded = null;
        public static event ClusterPosEventCallback ClusterDirty = null;
        public static event ClusterPosEventCallback ClusterRemoved = null;

        public delegate Material BindTextureCallback(string texture);
        public static BindTextureCallback BindTexture = null;

        public const int EmptyBlockIndex = 0;

        public class WorldInfo
        {
            public List<string> TextureNames = new List<string>();

            public string Name = string.Empty;
            public string Author = string.Empty;
            public string Site = string.Empty;

            public Vector3 SunPosition = new Vector3(200, 150, 100);
            public float Ambient = 0.5f;
            public float SunLuminance = 1.0f;
        }
        public static WorldInfo Info = new WorldInfo();

        public static List<Material> TextureMaterials = new List<Material>();

        public static List<BlockDef> BlockDefs = new List<BlockDef>();
        public static List<Block> BlockIndexCache = new List<Block>();

        public static Dictionary<ClusterPos, Cluster> Clusters = new Dictionary<ClusterPos, Cluster>();

        public static int AddTexture(string name)
        {
            int index = Info.TextureNames.IndexOf(name);
            if (index >= 0)
                return index;

            Info.TextureNames.Add(name);
            int ret = Info.TextureNames.Count - 1;

            TextureAdded?.Invoke(ret);
            if (BindTexture != null)
                TextureMaterials.Add(BindTexture(name));
            else
                TextureMaterials.Add(Urho.CoreAssets.Materials.DefaultGrey);

            return ret;
        }

        public static void BindAllTextures()
        {
            TextureMaterials.Clear();
            foreach (var name in Info.TextureNames)
            {
                if (BindTexture != null)
                    TextureMaterials.Add(BindTexture(name));
                else
                    TextureMaterials.Add(Urho.CoreAssets.Materials.DefaultGrey);
            }
        }

        public static int AddBlockDef(BlockDef def)
        {
            BlockDefs.Add(def);
            int ret = BlockDefs.Count - 1;
            BlockDefAdded?.Invoke(ret);
            return ret;
        }

        public static ushort AddBlock(Block blocInfo)
        {
            lock (BlockIndexCache)
            {
                if (blocInfo == Block.Empty)
                    return 0;

                if (blocInfo == Block.Invalid)
                    throw new Exception("Can not add invalid block to cluster");

                int index = BlockIndexCache.FindIndex(x => x.Equals(blocInfo));
                if (index >= 0)
                    return (ushort)(index + 1);

                BlockIndexCache.Add(blocInfo);
                ushort ret = (ushort)BlockIndexCache.Count();
                BlockIndexAdded?.Invoke(ret);
                return ret;
            }
        }

        public static Block GetBlock(ushort index)
        {
            if (index == EmptyBlockIndex || index > BlockIndexCache.Count)
                return Block.Empty;

            return BlockIndexCache[index - 1];
        }

        public static void Clear()
        {
            BlockDefs.Clear();
            BlockIndexCache.Clear();
            Clusters.Clear();
            Info = new WorldInfo();
        }

        public static Int64 AxisToGrid(Int64 value)
        {
           if (value >= 0)
              return (value / Cluster.HVSize) * Cluster.HVSize;

           return (((value +1) - Cluster.HVSize) / Cluster.HVSize) * Cluster.HVSize;
        }

        public static Vector3 PositionToBlock(Vector3 pos)
        {
            return new Vector3((float)Math.Floor(pos.X), (float)Math.Floor(pos.Y), (float)Math.Floor(pos.Z));
        }

        public static Cluster NeighborCluster(ClusterPos origin, int offsetH, int offsetV, int offsetD)
        {
            ClusterPos pos = origin.OffsetGrid(offsetH,offsetV);

            if (!Clusters.ContainsKey(pos))
                return null;

            return Clusters[pos];
        }

        public static Block BlockFromPosition(Int64 h, Int64 v, Int64 d)
        {
            if (d >= Cluster.DSize || d < 0)
                return Block.Invalid;

            ClusterPos pos = new ClusterPos(AxisToGrid(h), AxisToGrid(v));

            if (!Clusters.ContainsKey(pos))
                return Block.Invalid;

            return Clusters[pos].GetBlockAbs(h, v, d);
        }

        public static Block BlockFromRelativePosition(Cluster cluster, Int64 h, Int64 v, Int64 d)
        {
            if (d >= Cluster.DSize || d < 0)
                return Block.Invalid;

            if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < Cluster.HVSize)
                return cluster.GetBlockRelative(h, v, d);

            ClusterPos pos = new ClusterPos(AxisToGrid(cluster.Origin.H + h), AxisToGrid(cluster.Origin.V + v));

            if (!Clusters.ContainsKey(pos))
                return Block.Invalid;

            return Clusters[pos].GetBlockAbs(cluster.Origin.H + h, cluster.Origin.V + v, d);
        }

        public static Block BlockFromPosition(float h, float v, float d)
        {
            return BlockFromPosition((Int64)h, (Int64)v, (Int64)d);
        }

        public static Block BlockFromPosition(Vector3 pos)
        {
            return BlockFromPosition((Int64)pos.X, (Int64)pos.Z, (Int64)pos.Y);
        }

        public static Cluster ClusterFromPosition(Int64 h, Int64 v, Int64 d)
        {
            return ClusterFromPosition(new ClusterPos(AxisToGrid(h), AxisToGrid(v)));
        }

        public static Cluster ClusterFromPosition(ClusterPos pos)
        {
            if (!Clusters.ContainsKey(pos))
                return null;

            return Clusters[pos];
        }

        public static Cluster ClusterFromPosition(float h, float v, float d)
        {
            return ClusterFromPosition((Int64)h, (Int64)v, (Int64)d);
        }

        public static Cluster ClusterFromPosition(Vector3 pos)
        {
            return ClusterFromPosition((Int64)pos.X, (Int64)pos.Z, (Int64)pos.Y);
        }

        public static bool PositionIsOffMap(float h, float v, float d)
        {
            return PositionIsOffMap((Int64)h, (Int64)v, (Int64)d);
        }

        public static bool PositionIsOffMap(Vector3 pos)
        {
            return PositionIsOffMap((Int64)pos.X, (Int64)pos.Z, (Int64)pos.Y);
        }

        public static bool PositionIsOffMap(Int64 h, Int64 v, Int64 d)
        {
            if (d >= Cluster.DSize || d < 0)
                return true;

            ClusterPos pos = new ClusterPos(AxisToGrid(h), AxisToGrid(v));

            if (!Clusters.ContainsKey(pos))
                return true;

            return false;
        }

        public static float DropDepth(Vector2 position)
        {
            return DropDepth(position.X, position.Y);
        }

        public static float DropDepth(float positionH, float positionV)
        {
            ClusterPos pos = new ClusterPos(AxisToGrid((int)positionH), AxisToGrid((int)positionV));
            if (!Clusters.ContainsKey(pos))
                return float.MinValue;

            Cluster c = Clusters[pos];
            return c.DropDepth(positionH-pos.H,positionV - pos.V);
        }
    }
}

