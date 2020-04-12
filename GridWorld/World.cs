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
            if (index == 0 || index > BlockIndexCache.Count)
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

        private static Dictionary<Block.Geometry, Plane> CollisionPlanes = new Dictionary<Block.Geometry, Plane>();

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

        public static int AxisToGrid(int value)
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

        public static Block BlockFromPosition(int h, int v, int d)
        {
            if (d >= Cluster.DSize || d < 0)
                return Block.Invalid;

            ClusterPos pos = new ClusterPos(AxisToGrid(h), AxisToGrid(v));

            if (!Clusters.ContainsKey(pos))
                return Block.Invalid;

            return Clusters[pos].GetBlockAbs(h, v, d);
        }

        public static Block BlockFromRelativePosition(Cluster cluster, int h, int v, int d)
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
            return BlockFromPosition((int)h, (int)v, (int)d);
        }

        public static Block BlockFromPosition(Vector3 pos)
        {
            return BlockFromPosition((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public static Cluster ClusterFromPosition(int h, int v, int d)
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
            return ClusterFromPosition((int)h, (int)v, (int)d);
        }

        public static Cluster ClusterFromPosition(Vector3 pos)
        {
            return ClusterFromPosition((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public static bool PositionIsOffMap(float h, float v, float d)
        {
            return PositionIsOffMap((int)h, (int)v, (int)d);
        }

        public static bool PositionIsOffMap(Vector3 pos)
        {
            return PositionIsOffMap((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public static bool PositionIsOffMap(int h, int v, int d)
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
            int x = (int)positionH - pos.H;
            int y = (int)positionV - pos.V;

            float blockH = positionH - pos.H;
            float blockV = positionV - pos.V;

            for (int d = Cluster.DSize - 1; d >= 0; d--)
            {
                float value = c.GetBlockRelative(x, y, d).GetDForLocalPosition(blockH - x, blockV - y);
                if (value != float.MinValue)
                    return d + value;
            }

            return float.MinValue;
        }

        public static bool BlockPositionIsPassable(Vector3 pos)
        {
            return BlockPositionIsPassable(pos, null);
        }

        private static void CheckPlanes()
        {
            if (CollisionPlanes.Count != 0)
                return;

            Vector3 vec = new Vector3(0, 1, -1);
            vec.Normalize();

            CollisionPlanes.Add(Block.Geometry.NorthFullRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0, 1, 1);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.SouthFullRamp, new Plane(new Vector4(vec, 1)));

            vec = new Vector3(-1, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.EastFullRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(1, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.WestFullRamp, new Plane(new Vector4(vec, 1)));


            vec = new Vector3(0, 1, -0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.NorthHalfLowerRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0, 1, 0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.SouthHalfLowerRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(-0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.EastHalfLowerRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.WestHalfLowerRamp, new Plane(new Vector4(vec, 0.5f)));


            vec = new Vector3(0, 1, -0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.NorthHalfUpperRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(0, 1, 0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.SouthHalfUpperRamp, new Plane(new Vector4(vec, 1)));

            vec = new Vector3(-0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.EastHalfUpperRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Block.Geometry.WestHalfUpperRamp, new Plane(new Vector4(vec, 1)));
        }

        public static bool BlockPositionIsPassable(Vector3 pos, Block block, Vector3 blockPos, CollisionInfo info)
        {
            CheckPlanes();

            if (block == Block.Empty || block == Block.Invalid || block.Geom == Block.Geometry.Empty)
                return true;

            if (block.Geom == Block.Geometry.Solid)
                return false;

            if (block.Geom == Block.Geometry.Fluid)
                return true;

            int H = (int)blockPos.X;
            int V = (int)blockPos.Z;
            int D = (int)blockPos.Y;

            Vector3 relPos = pos - blockPos;

            switch (block.Geom)
            {
                case Block.Geometry.HalfUpper:
                    return relPos.Y < 0.5f;

                case Block.Geometry.HalfLower:
                    return relPos.Y >= 0.5f;
            }

            Plane plane = CollisionPlanes[block.Geom];

            if (info != null)
                info.ClipPlane = plane;

            if (plane.IntersectsPoint(relPos) == PlaneIntersectionType.Front)
                return true;

            return false;
        }

        public static bool BlockPositionIsPassable(Vector3 pos, CollisionInfo info)
        {
            Block block = BlockFromPosition(pos);
            Vector3 blockPos = PositionToBlock(pos);

            return BlockPositionIsPassable(pos, block, blockPos, info);
        }

        public class CollisionInfo
        {
            public Block CollidedBlock = Block.Empty;
            public float Lenght = 0f;
            public Vector3 CollidedBlockPosition = Vector3.Zero;
            public Vector3 CollisionLocation = Vector3.Zero;

            public Plane ClipPlane = new Plane();
            public float StartLen = 0;

            public bool Collided = true;

            public CollisionInfo NoCollide() { Collided = false; return this; }
        }

        public static  CollisionInfo LineCollidesWithWorld(Vector3 start, Vector3 end)
        {
            CollisionInfo info = new CollisionInfo();

            // figure out the axis with the longest delta
            Vector3 delta = end - start;
            float lenght = delta.Length;
            delta.Normalize();
            int axis = 0;
            float max = Math.Abs(delta.X);
            if (Math.Abs(delta.Z) > max)
            {
                axis = 2;
                max = Math.Abs(delta.Z);
            }
            if (Math.Abs(delta.Y) > max)
            {
                axis = 1;
                max = Math.Abs(delta.Y);
            }

            info.CollidedBlock = BlockFromPosition(start);
            info.CollidedBlockPosition = PositionToBlock(start);
            info.CollisionLocation = start;

            if (max < 0.001f) // the ray is too small to test in any axis
            {
                if (BlockPositionIsPassable(start))
                    return info.NoCollide();
                else
                    return info;
            }

            if (!BlockPositionIsPassable(start))
                return info;

            info.CollidedBlock = BlockFromPosition(end);
            info.Lenght = lenght;
            info.CollidedBlockPosition = PositionToBlock(end);
            info.CollisionLocation = end; // TODO, run the vector back untill an axis hits a valid number so we get the first edge hit

            if (!BlockPositionIsPassable(end))
                return info;

            Vector3 NewStart = Vector3.Zero;
            Vector3 newDelta = Vector3.Zero;

            float maxSegments = 0;

            float newStartParam = 0;
            // find the start point;
            if (axis == 0)
            {
                newStartParam = (((int)start.X + 1) - start.X) / delta.X;
                NewStart = start + delta * newStartParam;

                float param = 1f / delta.X;
                newDelta = delta * param;

                maxSegments = Math.Abs(end.X - start.X);
            }
            else if (axis == 1)
            {
                newStartParam = (((int)start.Y + 1) - start.Y) / delta.Y;
                NewStart = start + delta * newStartParam;

                float param = 1f / delta.Y;
                newDelta = delta * param;

                maxSegments = Math.Abs(end.Y - start.Y);
            }
            else
            {
                newStartParam = (((int)start.Z + 1) - start.Z) / delta.Z;
                NewStart = start + delta * newStartParam;

                float param = 1f / delta.Z;
                newDelta = delta * param;

                maxSegments = Math.Abs(end.Z - start.Z);
            }

            info.StartLen = newStartParam;
            info.CollidedBlockPosition = PositionToBlock(NewStart);
            info.CollisionLocation = NewStart; // TODO run back
            info.CollidedBlock = BlockFromPosition(NewStart);
            info.Lenght = newStartParam;

            if (!BlockPositionIsPassable(NewStart))
                return info;

            float deltaLen = newDelta.Length;

            Vector3 lastPos = NewStart;

            for (float f = 1; f < maxSegments; f += 1f)
            {
                NewStart += newDelta;
                Vector3 blockPos = PositionToBlock(NewStart);

                if (PositionIsOffMap(blockPos)) // we are done as soon as we go off the map
                    return info.NoCollide();

                Block block = BlockFromPosition(blockPos);

                info.CollidedBlock = block;
                info.CollidedBlockPosition = blockPos;
                info.CollisionLocation = lastPos; // TODO run back

                if (!BlockPositionIsPassable(lastPos, block, blockPos, info))
                    return info;

                info.Lenght += deltaLen;
                info.CollisionLocation = NewStart;
                if (!BlockPositionIsPassable(NewStart, block, blockPos, info))
                    return info;

                lastPos = NewStart;
            }

            return info.NoCollide();
        }
    }
}

