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
    public static class World
    { 
        public static string WorldFileExtension = "world";
        public static string ClusterFileExtension = "cluster";
        public static string GeometryFileExtension = "Geom";

        public static bool CompressFileIO = true;

        public static List<Block> BlockIndexCache = new List<Block>();

        public const int EmptyBlockIndex = 0;

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
                return (ushort)BlockIndexCache.Count();
            }
        }

        public static Block GetBlock(ushort index)
        {
            if (index == EmptyBlockIndex || index > BlockIndexCache.Count)
                return Block.Empty;

            return BlockIndexCache[index - 1];
        }

        public class TextureInfo
        {
            public override string ToString()
            {
                return FileName;
            }

            public string FileName = string.Empty;
            public int HCount = 16;
            public int VCount = 16;

            public TextureInfo() { }

            public TextureInfo(string name)
            {
                FileName = name;
                HCount = 1;
                VCount = 1;
            }

            public TextureInfo(string name, int h, int v)
            {
                FileName = name;
                HCount = h;
                VCount = v;
            }

            [XmlIgnore]
            public int Start = -1;

            [XmlIgnore]
            public int End = -1;


            [XmlIgnore]
            public Urho.Material RuntimeMat = null;
        }

        public class WorldInfo
        {
            public List<TextureInfo> Textures = new List<TextureInfo>();
            public string Name = string.Empty;
            public string Author = string.Empty;
            public string Site = string.Empty;

            public Vector3 SunPosition = new Vector3(200, 150, 100);
            public float Ambient = 0.5f;
            public float SunLuminance = 1.0f;
        }
        public static WorldInfo Info = new WorldInfo();

        private static Dictionary<Block.Geometries, Plane> CollisionPlanes = new Dictionary<Block.Geometries, Plane>();

        public static void SetupTextureInfos()
        {
            int count = 0;
            foreach (TextureInfo info in Info.Textures)
            {
                info.Start = count;
                count += info.HCount * info.VCount;
                info.End = count - 1;
            }
        }

        private static void CheckTextureInfos()
        {
            if (Info.Textures.Count > 0 && Info.Textures[0].End == -1)
                SetupTextureInfos();
        }

        public static int BlockTextureToTextureID(int blockTexture)
        {
            CheckTextureInfos();

            for (int i = 0; i < Info.Textures.Count; i++)
            {
                if (blockTexture >= Info.Textures[i].Start && blockTexture <= Info.Textures[i].End)
                    return i;
            }

            return -1;
        }

        public static int BlockTextureToTextureOffset(int blockTexture)
        {
            int texture = BlockTextureToTextureID(blockTexture);
            if (texture < 0)
                return -1;

            return blockTexture - Info.Textures[texture].Start;
        }

        public class BlockDef
        {
            public string Name = string.Empty;

            public override string ToString()
            {
                return Name;
            }

            // defines the textures used
            public int Top = -1;
            public int[] Sides = null;
            public int Bottom = -1;

            public bool Transperant = false;

            public BlockDef() { }

            public BlockDef(string name, int top)
            {
                Name = name;
                Top = top;
            }

            public BlockDef(string name, int top, int sides)
            {
                Name = name;
                Top = top;
                Sides = new int[1];
                Sides[0] = sides;
            }

            public BlockDef(string name, int top, int sides, int bottom)
            {
                Name = name;
                Top = top;
                Sides = new int[1];
                Sides[0] = sides;
                Bottom = bottom;
            }

            public BlockDef(string name, int top, int north, int south, int east, int west, int bottom)
            {
                Name = name;
                Top = top;
                Sides = new int[4];
                Sides[0] = north;
                Sides[1] = south;
                Sides[2] = east;
                Sides[3] = west;

                Bottom = bottom;
            }

            public static int EmptyID = -1;
        }

        public static List<BlockDef> BlockDefs = new List<BlockDef>();

        public static int AddBlockDef(BlockDef def)
        {
            BlockDefs.Add(def);
            return BlockDefs.Count - 1;
        }

        public static Dictionary<ClusterPos, Cluster> Clusters = new Dictionary<ClusterPos, Cluster>();


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
//             Int64 x = (Int64)positionH - pos.H;
//             Int64 y = (Int64)positionV - pos.V;
// 
//             float blockH = positionH - pos.H;
//             float blockV = positionV - pos.V;
// 
//             for (int d = Cluster.DSize - 1; d >= 0; d--)
//             {
//                 float value = c.GetBlockRelative(x, y, d).GetDForLocalPosition(blockH - x, blockV - y);
//                 if (value != float.MinValue)
//                     return d + value;
//             }

            return c.DropDepth(positionH-pos.H,positionV - pos.V);
        }
    }
}

