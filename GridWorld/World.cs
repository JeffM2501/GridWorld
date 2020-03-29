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
    public class World
    { 
        public static string WorldFileExtension = "world";
        public static string ClusterFileExtension = "cluster";
        public static string GeometryFileExtension = "Geom";

        public static bool CompressFileIO = true;

        public static World Empty = new World();

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
        public WorldInfo Info = new WorldInfo();

        [XmlIgnore]
        protected Dictionary<Cluster.Block.Geometry, Plane> CollisionPlanes = new Dictionary<Cluster.Block.Geometry, Plane>();

        public void SetupTextureInfos()
        {
            int count = 0;
            foreach (TextureInfo info in Info.Textures)
            {
                info.Start = count;
                count += info.HCount * info.VCount;
                info.End = count - 1;
            }
        }

        protected void CheckTextureInfos()
        {
            if (Info.Textures.Count > 0 && Info.Textures[0].End == -1)
                SetupTextureInfos();
        }

        public int BlockTextureToTextureID(int blockTexture)
        {
            CheckTextureInfos();

            for (int i = 0; i < Info.Textures.Count; i++)
            {
                if (blockTexture >= Info.Textures[i].Start && blockTexture <= Info.Textures[i].End)
                    return i;
            }

            return -1;
        }

        public int BlockTextureToTextureOffset(int blockTexture)
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

        public List<BlockDef> BlockDefs = new List<BlockDef>();

        public int AddBlockDef(BlockDef def)
        {
            BlockDefs.Add(def);
            return BlockDefs.Count - 1;
        }

        [XmlIgnore]
        protected OctreeRoot octree;

        [XmlIgnore]
        public Dictionary<Cluster.ClusterPos, Cluster> Clusters = new Dictionary<Cluster.ClusterPos, Cluster>();

        public void Serialize(FileInfo location)
        {
            if (location.Exists)
                location.Delete();
            FileStream fs = location.OpenWrite();
            XmlSerializer XML = new XmlSerializer(typeof(World));
            if (CompressFileIO)
            {
                GZipStream gz = new GZipStream(fs, CompressionMode.Compress);
                XML.Serialize(gz, this);
                gz.Close();
            }
            else
                XML.Serialize(fs, this);

            fs.Close();
        }

        public static World Deserialize(FileInfo location)
        {
            if (!location.Exists)
                return World.Empty;

            try
            {
                FileStream fs = location.OpenRead();
                XmlSerializer XML = new XmlSerializer(typeof(World));
                World world = null;
                if (CompressFileIO)
                {
                    GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                    world = (World)XML.Deserialize(gz);
                    gz.Close();
                }
                else
                    world = (World)XML.Deserialize(fs);

                fs.Close();
                return world;
            }
            catch (System.Exception /*ex*/)
            {
            }

            return World.Empty;
        }

        public static World ReadWorldAndClusters(FileInfo location)
        {
            World world = Deserialize(location);
            if (world == World.Empty)
                return World.Empty;

            DirectoryInfo dir = new DirectoryInfo(location.DirectoryName);
            foreach (FileInfo file in dir.GetFiles("*." + ClusterFileExtension))
            {
                try
                {
                    FileStream fs = file.OpenRead();
                    XmlSerializer XML = new XmlSerializer(typeof(Cluster));
                    Cluster cluster = null;
                    if (CompressFileIO)
                    {
                        GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                        cluster = (Cluster)XML.Deserialize(gz);
                        gz.Close();
                    }
                    else
                        cluster = (Cluster)XML.Deserialize(fs);
                    fs.Close();

                    world.Clusters.Add(cluster.Origin, cluster);
                }
                catch (System.Exception /*ex*/)
                {

                }
            }
            world.Finailize();
            return world;
        }

        public static World ReadWorldWithGeometry(FileInfo location)
        {
            World world = ReadWorldAndClusters(location);
            if (world == World.Empty)
                return World.Empty;

            DirectoryInfo dir = new DirectoryInfo(location.DirectoryName);
            foreach (FileInfo file in dir.GetFiles("*." + GeometryFileExtension))
            {
                try
                {
                    ClusterGeometry geometry = ClusterGeometry.Deserialize(file);
                    if (world.Clusters.ContainsKey(geometry.ClusterOrigin))
                        world.Clusters[geometry.ClusterOrigin].Geometry = geometry;
                }
                catch (System.Exception /*ex*/)
                {

                }
            }
            return world;
        }

        public void SaveWorldAndClusters(FileInfo location)
        {
            Serialize(location);

            // kill all clusters in that folder
            foreach (FileInfo clusterFile in location.Directory.GetFiles("*." + ClusterFileExtension))
                clusterFile.Delete();

            foreach (Cluster c in Clusters.Values)
            {
                FileInfo file = new FileInfo(Path.Combine(location.DirectoryName, c.Origin.ToString() + "." + ClusterFileExtension));
                FileStream fs = file.OpenWrite();

                XmlSerializer XML = new XmlSerializer(typeof(Cluster));
                if (CompressFileIO)
                {
                    GZipStream gz = new GZipStream(fs, CompressionMode.Compress);
                    XML.Serialize(gz, c);
                    gz.Close();
                }
                else
                    XML.Serialize(fs, c);

                fs.Close();
            }
        }

        public void SaveWorldWithGeometry(FileInfo location)
        {
            SaveWorldAndClusters(location);

            // kill all clusters in that folder
            foreach (FileInfo clusterFile in location.Directory.GetFiles("*." + GeometryFileExtension))
                clusterFile.Delete();

            foreach (Cluster c in Clusters.Values)
                c.Geometry.Serialize(new FileInfo(Path.Combine(location.DirectoryName, c.Origin.ToString() + "." + GeometryFileExtension)));
        }

        public List<Cluster> ClustersInFrustum(Frustum frustum, bool useOctree)
        {
            // super cheap
            List<Cluster> vis = new List<Cluster>();

            if (useOctree && octree != null)
                return InFrustum<Cluster>(frustum);
            else
            {
                foreach (KeyValuePair<Cluster.ClusterPos, Cluster> item in Clusters)
                {
                    if (frustum.Contains(item.Value.Bounds) != ContainmentType.Disjoint)
                        vis.Add(item.Value);
                }
            }
            return vis;
        }

        public void Finailize()
        {
            octree = new OctreeRoot();
            octree.Add(Clusters.Values);
        }

        public void AddObject(IOctreeObject obj)
        {
            if (octree != null)
                octree.Add(obj);
        }

        public void RemoveObject(IOctreeObject obj)
        {
            if (octree != null)
                octree.FastRemove(obj);
        }

        public List<T> InFrustum<T>(Frustum frustum) where T : IOctreeObject
        {
            List<T> objects = new List<T>();

            if (octree != null)
            {
                List<object> v = new List<object>();
                octree.ObjectsInFrustum(v, frustum);
                foreach (object c in v)
                {
                    if (c.GetType() == typeof(T) || c.GetType().IsSubclassOf(typeof(T)))
                        objects.Add((T)c);
                }
            }
            return objects;
        }

        public List<T> InBoundingBox<T>(BoundingBox box) where T : IOctreeObject
        {
            List<T> objects = new List<T>();

            if (octree != null)
            {
                List<object> v = new List<object>();
                octree.ObjectsInBoundingBox(v, box);
                foreach (T c in v)
                    objects.Add(c);
            }
            return objects;
        }

        public List<T> InBoundingSphere<T>(SphereShape sphere) where T : IOctreeObject
        {
            List<T> objects = new List<T>();

            if (octree != null)
            {
                List<object> v = new List<object>();
                octree.ObjectsInBoundingSphere(v, sphere);
                foreach (T c in v)
                    objects.Add(c);
            }
            return objects;
        }

        protected int AxisToGrid(int value)
        {
            if (value >= 0)
                return (value / Cluster.HVSize) * Cluster.HVSize;

            return ((value - Cluster.HVSize) / Cluster.HVSize) * Cluster.HVSize;
        }

        public static Vector3 PositionToBlock(Vector3 pos)
        {
            return new Vector3((float)Math.Floor(pos.X), (float)Math.Floor(pos.Y), (float)Math.Floor(pos.Z));
        }

        public Cluster.Block BlockFromPosition(int h, int v, int d)
        {
            if (d >= Cluster.DSize || d < 0)
                return Cluster.Block.Invalid;

            Cluster.ClusterPos pos = new Cluster.ClusterPos(AxisToGrid(h), AxisToGrid(v));

            if (!Clusters.ContainsKey(pos))
                return Cluster.Block.Invalid;

            return Clusters[pos].GetBlockAbs(h, v, d);
        }

        public Cluster.Block BlockFromPosition(float h, float v, float d)
        {
            return BlockFromPosition((int)h, (int)v, (int)d);
        }

        public Cluster.Block BlockFromPosition(Vector3 pos)
        {
            return BlockFromPosition((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public Cluster ClusterFromPosition(int h, int v, int d)
        {
            if (d >= Cluster.DSize || d < 0)
                return null;
            return ClusterFromPosition(new Cluster.ClusterPos(AxisToGrid(h), AxisToGrid(v)));
        }

        public Cluster ClusterFromPosition(Cluster.ClusterPos pos)
        {
            if (!Clusters.ContainsKey(pos))
                return null;

            return Clusters[pos];
        }

        public Cluster ClusterFromPosition(float h, float v, float d)
        {
            return ClusterFromPosition((int)h, (int)v, (int)d);
        }

        public Cluster ClusterFromPosition(Vector3 pos)
        {
            return ClusterFromPosition((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public bool PositionIsOffMap(float h, float v, float d)
        {
            return PositionIsOffMap((int)h, (int)v, (int)d);
        }

        public bool PositionIsOffMap(Vector3 pos)
        {
            return PositionIsOffMap((int)pos.X, (int)pos.Z, (int)pos.Y);
        }

        public bool PositionIsOffMap(int h, int v, int d)
        {
            if (d >= Cluster.DSize || d < 0)
                return true;

            Cluster.ClusterPos pos = new Cluster.ClusterPos(AxisToGrid(h), AxisToGrid(v));

            if (!Clusters.ContainsKey(pos))
                return true;

            return false;
        }

        public float DropDepth(Vector2 position)
        {
            return DropDepth(position.X, position.Y);
        }

        public float DropDepth(float positionH, float positionV)
        {
            Cluster.ClusterPos pos = new Cluster.ClusterPos(AxisToGrid((int)positionH), AxisToGrid((int)positionV));
            if (!Clusters.ContainsKey(pos))
                return float.MinValue;

            Cluster c = Clusters[pos];
            int x = (int)positionH - pos.H;
            int y = (int)positionV - pos.V;

            float blockH = positionH - pos.H;
            float blockV = positionV - pos.V;

            for (int d = Cluster.DSize - 1; d >= 0; d--)
            {
                float value = c.GetBlockRelative(x, y, d).GetDForLocalPosition(blockH, blockV);
                if (value != float.MinValue)
                    return d + value;
            }

            return float.MinValue;
        }

        public bool BlockPositionIsPassable(Vector3 pos)
        {
            return BlockPositionIsPassable(pos, null);
        }

        protected void CheckPlanes()
        {
            if (CollisionPlanes.Count != 0)
                return;

            Vector3 vec = new Vector3(0, 1, -1);
            vec.Normalize();

            CollisionPlanes.Add(Cluster.Block.Geometry.NorthFullRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0, 1, 1);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.SouthFullRamp, new Plane(new Vector4(vec, 1)));

            vec = new Vector3(-1, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.EastFullRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(1, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.WestFullRamp, new Plane(new Vector4(vec, 1)));


            vec = new Vector3(0, 1, -0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.NorthHalfLowerRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0, 1, 0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.SouthHalfLowerRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(-0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.EastHalfLowerRamp, new Plane(new Vector4(vec, 0)));

            vec = new Vector3(0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.WestHalfLowerRamp, new Plane(new Vector4(vec, 0.5f)));


            vec = new Vector3(0, 1, -0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.NorthHalfUpperRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(0, 1, 0.5f);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.SouthHalfUpperRamp, new Plane(new Vector4(vec, 1)));

            vec = new Vector3(-0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.EastHalfUpperRamp, new Plane(new Vector4(vec, 0.5f)));

            vec = new Vector3(0.5f, 1, 0);
            vec.Normalize();
            CollisionPlanes.Add(Cluster.Block.Geometry.WestHalfUpperRamp, new Plane(new Vector4(vec, 1)));
        }

        public bool BlockPositionIsPassable(Vector3 pos, Cluster.Block block, Vector3 blockPos, CollisionInfo info)
        {
            CheckPlanes();

            if (block == Cluster.Block.Empty || block == Cluster.Block.Invalid || block.Geom == Cluster.Block.Geometry.Empty)
                return true;

            if (block.Geom == Cluster.Block.Geometry.Solid)
                return false;

            if (block.Geom == Cluster.Block.Geometry.Fluid)
                return true;

            int H = (int)blockPos.X;
            int V = (int)blockPos.Z;
            int D = (int)blockPos.Y;

            Vector3 relPos = pos - blockPos;

            switch (block.Geom)
            {
                case Cluster.Block.Geometry.HalfUpper:
                    return relPos.Y < 0.5f;

                case Cluster.Block.Geometry.HalfLower:
                    return relPos.Y >= 0.5f;
            }

            Plane plane = CollisionPlanes[block.Geom];

            if (info != null)
                info.ClipPlane = plane;

            if (plane.IntersectsPoint(relPos) == PlaneIntersectionType.Front)
                return true;

            return false;
        }

        public bool BlockPositionIsPassable(Vector3 pos, CollisionInfo info)
        {
            Cluster.Block block = BlockFromPosition(pos);
            Vector3 blockPos = PositionToBlock(pos);

            return BlockPositionIsPassable(pos, block, blockPos, info);
        }

        public class CollisionInfo
        {
            public Cluster.Block CollidedBlock = Cluster.Block.Empty;
            public float Lenght = 0f;
            public Vector3 CollidedBlockPosition = Vector3.Zero;
            public Vector3 CollisionLocation = Vector3.Zero;

            public Plane ClipPlane = new Plane();
            public float StartLen = 0;

            public bool Collided = true;

            public CollisionInfo NoCollide() { Collided = false; return this; }
        }

        public CollisionInfo LineCollidesWithWorld(Vector3 start, Vector3 end)
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

                Cluster.Block block = BlockFromPosition(blockPos);

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

