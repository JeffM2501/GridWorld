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
    public class ClusterGeometry
    {
        public ClusterPos ClusterOrigin = ClusterPos.Zero;

        public static ClusterGeometry Empty = new ClusterGeometry();
        public class Face
        {
            public Vector3[] Verts = null;
            public Vector3 Normal = Vector3.UnitZ;
            public Vector2[] UVs = null;

            public static Face Empty = new Face();

            public float[] Luminance = new float[] { 1, 1, 1, 1 };
        }

        public class MeshGroup
        {
            public int TextureID = 0;
            public List<VertexBuffer.PositionNormalColorTexcoord> Buffer = new List<VertexBuffer.PositionNormalColorTexcoord>();
            public List<short> IndexData = new List<short>();


            private static uint White = Urho.Color.White.ToUInt();

            private short VIndex = 0;

            public MeshGroup() { }
            public MeshGroup(int ID) { TextureID = ID; }

            public void Add(Face face)
            {
                if (face.Verts == null)
                    return;

                Vector3 normal = face.Normal;
                IndexData.Add(VIndex); VIndex++;
                Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[2], Normal = normal, Color = White, TexCoord = face.UVs[2] });

                IndexData.Add(VIndex); VIndex++;
                Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[1], Normal = normal, Color = White, TexCoord = face.UVs[1] });

                IndexData.Add(VIndex); VIndex++;
                Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[0], Normal = normal, Color = White, TexCoord = face.UVs[0] });
                
                if (face.Verts.Length == 4)
                {
                    IndexData.Add(VIndex); VIndex++;
                    if (face.UVs.Length == 3)
                        Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[3], Normal = normal, Color = White, TexCoord = face.UVs[2] });
                    else
                        Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[3], Normal = normal, Color = White, TexCoord = face.UVs[3] });

                    IndexData.Add(VIndex); VIndex++;
                    Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[2], Normal = normal, Color = White, TexCoord = face.UVs[2] });

                    IndexData.Add(VIndex); VIndex++;
                    Buffer.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[0], Normal = normal, Color = White, TexCoord = face.UVs[0] });
                }
            }

            VertexBuffer UrhoBufferCache = null;
            IndexBuffer UrhoIndexCache = null;

            public Tuple<Urho.Geometry, int> UrhoGeo = null;

            public void FinalizeGeo()
            {
                if (UrhoBufferCache != null)
                    UrhoBufferCache.Dispose();
                if (UrhoIndexCache != null)
                    UrhoIndexCache.Dispose();

                UrhoBufferCache = new VertexBuffer(Application.Current.Context);
                UrhoIndexCache = new IndexBuffer(Application.Current.Context);

                UrhoBufferCache.Shadowed = true;

                UrhoBufferCache.SetSize((uint)Buffer.Count, ElementMask.Position | ElementMask.Normal | ElementMask.Color | ElementMask.TexCoord1, false);
                UrhoBufferCache.SetData(Buffer.ToArray());

                UrhoIndexCache.Shadowed = true;
                UrhoIndexCache.SetSize((uint)IndexData.Count, false);
                UrhoIndexCache.SetData(IndexData.ToArray());

                Urho.Geometry geo = new Urho.Geometry();
                geo.SetVertexBuffer(0, UrhoBufferCache);
                geo.IndexBuffer = UrhoIndexCache;
                geo.SetDrawRange(PrimitiveType.TriangleList, 0, (uint)Buffer.Count);

                UrhoGeo = new Tuple<Geometry, int>(geo, TextureID);

                Buffer.Clear();
                IndexData.Clear();
            }
        }

        [XmlIgnore]
        public Dictionary<int, MeshGroup> Meshes = new Dictionary<int, MeshGroup>();

        public MeshGroup GetMesh(int id)
        {
            if (!Meshes.ContainsKey(id))
                Meshes.Add(id, new MeshGroup(id));

            return Meshes[id];
        }

        public void Serialize(FileInfo location)
        {
            if (location.Exists)
                location.Delete();
            FileStream fs = location.OpenWrite();
            XmlSerializer XML = new XmlSerializer(typeof(ClusterGeometry));
            if (World.CompressFileIO)
            {
                GZipStream gz = new GZipStream(fs, CompressionMode.Compress);
                XML.Serialize(gz, this);
                gz.Close();
            }
            else
                XML.Serialize(fs, this);

            fs.Close();
        }

        public static ClusterGeometry Deserialize(FileInfo location)
        {
            if (!location.Exists)
                return ClusterGeometry.Empty;
            try
            {
                FileStream fs = location.OpenRead();

                XmlSerializer XML = new XmlSerializer(typeof(ClusterGeometry));

                ClusterGeometry geo = null;

                if (World.CompressFileIO)
                {
                    GZipStream gz = new GZipStream(fs, CompressionMode.Decompress);
                    geo = (ClusterGeometry)XML.Deserialize(gz);
                    gz.Close();
                }
                else
                    geo = (ClusterGeometry)XML.Deserialize(fs);
                fs.Close();

                return geo;
            }
            catch (System.Exception /*ex*/)
            {
            }

            return ClusterGeometry.Empty;
        }

        private void FinalizeGeo()
        {
//             foreach (var mesh in Meshes.Values)
//                 mesh.FinalizeGeo();
        }

        public List<Tuple<Urho.Geometry, int>> BindToUrhoGeo()
        {
            List<Tuple<Urho.Geometry, int>> geoList = new List<Tuple<Urho.Geometry, int>>();
            foreach (var mesh in Meshes.Values)
            {
                mesh.FinalizeGeo();
                geoList.Add(mesh.UrhoGeo);
            }
            Meshes.Clear();
            return geoList;
        }

        public static void BuildGeometry(World world)
        {
            GeometryBuilder.DoBuildGeometry();
        }

        public static void BuildGeometry(Cluster cluster)
        {
            GeometryBuilder.BuildGeometryForCluster(cluster);
        }

        public static class GeometryBuilder
        {
            public static World TheWorld = null;

            private static bool AboveIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                return otherGeo.Geom == Cluster.Block.Geometry.Fluid || otherGeo.Geom == Cluster.Block.Geometry.Empty || otherGeo.Geom == Cluster.Block.Geometry.HalfUpper;
            }

            private static bool IsLowerRamp(Cluster.Block thisGeo)
            {
                return thisGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.EastHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.WestHalfLowerRamp;
            }

            private static bool IsUpperRamp(Cluster.Block thisGeo)
            {
                return thisGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp;
            }

            private static bool BellowIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;


                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;
                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.HalfUpper && !IsLowerRamp(otherGeo) && !IsUpperRamp(otherGeo);
            }

            private static bool NorthIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.SouthFullRamp && otherGeo.Geom != Cluster.Block.Geometry.SouthHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.SouthHalfUpperRamp;
            }

            private static bool SouthIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;


                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.NorthFullRamp && otherGeo.Geom != Cluster.Block.Geometry.NorthHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.NorthHalfUpperRamp;
            }

            private static bool EastIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.EastHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.WestHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.WestFullRamp && otherGeo.Geom != Cluster.Block.Geometry.WestHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.WestHalfUpperRamp;
            }

            private static bool WestIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (otherGeo == Cluster.Block.Invalid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.WestHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.EastHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.EastFullRamp && otherGeo.Geom != Cluster.Block.Geometry.EastHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.EastHalfUpperRamp;
            }

            public static Vector2[] GetUVsForOffset(int imageOffset, int texture, World world)
            {
                World.TextureInfo info = world.Info.Textures[texture];

                int imageY = imageOffset / info.HCount;
                int imageX = imageOffset - imageY * info.HCount;

                double imageGirdX = 1.0 / info.HCount;
                double imageGirdY = 1.0 / info.VCount;

                Vector2[] ret = new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                //           return ret;
                ret[0] = new Vector2((float)(imageX * imageGirdX), (float)(imageY * imageGirdY));
                ret[1] = new Vector2((float)(imageX * imageGirdX) + (float)(imageGirdX), (float)(imageY * imageGirdY));
                ret[2] = new Vector2((float)(imageX * imageGirdX) + (float)(imageGirdX), (float)(imageY * imageGirdY) + (float)(imageGirdY));
                ret[3] = new Vector2((float)(imageX * imageGirdX), (float)(imageY * imageGirdY) + (float)(imageGirdY));

                return ret;
            }

            public static Vector2[] GetUVsForOffset(int imageOffset, int texture)
            {
                return GetUVsForOffset(imageOffset, texture, TheWorld);
            }

            private static Face BuildAboveGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildAboveGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildAboveGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                face.UVs = GetUVsForOffset(imageOffset, texture, world);

                switch (block.Geom)
                {
                    case Cluster.Block.Geometry.Empty:
                        return Face.Empty;

                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.HalfUpper:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Cluster.Block.Geometry.HalfLower:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Cluster.Block.Geometry.Fluid:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 0.95f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v + 1);
                        break;

                    case Cluster.Block.Geometry.NorthFullRamp:
                        face.Normal = new Vector3(0, 1, -1);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Cluster.Block.Geometry.SouthFullRamp:
                        face.Normal = new Vector3(0, 1, 1);

                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;

                    case Cluster.Block.Geometry.EastFullRamp:
                        face.Normal = new Vector3(-1, 1, 0);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d+ 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;


                    case Cluster.Block.Geometry.WestFullRamp:
                        face.Normal = new Vector3(1, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Cluster.Block.Geometry.NorthHalfLowerRamp:
                        face.Normal = new Vector3(0, 1, -0.5f);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Cluster.Block.Geometry.SouthHalfLowerRamp:
                        face.Normal = new Vector3(0, 1, 0.5f);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;

                    case Cluster.Block.Geometry.EastHalfLowerRamp:
                        face.Normal = new Vector3(-0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;


                    case Cluster.Block.Geometry.WestHalfLowerRamp:
                        face.Normal = new Vector3(0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                        face.Normal = new Vector3(0, 1, -0.5f);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1f, v + 1);
                        break;

                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        face.Normal = new Vector3(0, 1, 0.5f);

                        face.Verts[0] = new Vector3(h, d + 1f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Cluster.Block.Geometry.EastHalfUpperRamp:
                        face.Normal = new Vector3(-0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;


                    case Cluster.Block.Geometry.WestHalfUpperRamp:
                        face.Normal = new Vector3(0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 1f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1f, v + 1);
                        break;
                }

                face.Normal.Normalize();

                return face;
            }

            private static Face BuildBelowGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildBelowGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildBelowGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                if (block.Geom == Cluster.Block.Geometry.Empty)
                    return Face.Empty;

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture, world);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitY * -1.0f;

                float ZOffset = 0;
                if (block.Geom == Cluster.Block.Geometry.HalfUpper)
                    ZOffset = 0.5f;

                face.Verts[0] = new Vector3(h, d + ZOffset, v);
                face.Verts[1] = new Vector3(h, d + ZOffset, v + 1);
                face.Verts[2] = new Vector3(h + 1, d + ZOffset, v + 1);
                face.Verts[3] = new Vector3(h + 1, d + ZOffset, v);

                return face;
            }

            private static float RampCenterUOffset = 0.017f; //015625f;

            private static Face BuildNorthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildNorthGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildNorthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture, world);
                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Cluster.Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Cluster.Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Cluster.Block.Geometry.Empty:
                    case Cluster.Block.Geometry.SouthFullRamp:
                        return Face.Empty;

                    case Cluster.Block.Geometry.HalfUpper:
                    case Cluster.Block.Geometry.HalfLower:
                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.NorthFullRamp:
                    case Cluster.Block.Geometry.NorthHalfLowerRamp:
                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[3] = new Vector3(h, d + lower, v + 1);
                        face.Verts[0] = new Vector3(h, d + upper, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + upper, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + lower, v + 1);
                        break;

                    case Cluster.Block.Geometry.Fluid:
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0.95f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        break;

                    case Cluster.Block.Geometry.EastFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[1], face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset) };
                        break;

                    case Cluster.Block.Geometry.WestFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1], face.UVs[0] };
                        break;

                    case Cluster.Block.Geometry.EastHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[1], face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f) };
                        break;

                    case Cluster.Block.Geometry.WestHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1], face.UVs[0] };
                        break;

                    case Cluster.Block.Geometry.EastHalfUpperRamp:

                        face.Verts[3] = new Vector3(h, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0, v + 1);

                        break;

                    case Cluster.Block.Geometry.WestHalfUpperRamp:

                        face.Verts[3] = new Vector3(h, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h, d + 1, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0, v + 1);
                        break;
                }

                return face;
            }

            private static Face BuildSouthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildSouthGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildSouthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture, world);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ * -1;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Cluster.Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Cluster.Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Cluster.Block.Geometry.Empty:
                    case Cluster.Block.Geometry.NorthFullRamp:
                        return Face.Empty;

                    case Cluster.Block.Geometry.HalfLower:
                    case Cluster.Block.Geometry.HalfUpper:
                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.SouthFullRamp:
                    case Cluster.Block.Geometry.SouthHalfLowerRamp:
                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + lower, v);
                        face.Verts[1] = new Vector3(h + 1, d + lower,v);
                        face.Verts[2] = new Vector3(h + 1, d + upper, v);
                        face.Verts[3] = new Vector3(h, d + upper, v);
                        break;

                    case Cluster.Block.Geometry.Fluid:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.95f, v);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v);
                        break;

                    case Cluster.Block.Geometry.EastFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2] };
                        break;

                    case Cluster.Block.Geometry.WestFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h, d + 1, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.EastHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2] };
                        break;

                    case Cluster.Block.Geometry.WestHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.EastHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + 0, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v);
                        break;

                    case Cluster.Block.Geometry.WestHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + 0, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = new Vector3(h, d + 1, v);
                        break;
                }

                return face;
            }

            private static Face BuildEastGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildEastGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildEastGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture, world);
                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Cluster.Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Cluster.Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Cluster.Block.Geometry.WestHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Cluster.Block.Geometry.Empty:
                    case Cluster.Block.Geometry.WestFullRamp:
                        return Face.Empty;

                    case Cluster.Block.Geometry.HalfLower:
                    case Cluster.Block.Geometry.HalfUpper:
                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.EastFullRamp:
                    case Cluster.Block.Geometry.EastHalfLowerRamp:
                    case Cluster.Block.Geometry.EastHalfUpperRamp:
                    case Cluster.Block.Geometry.WestHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + lower, v);
                        face.Verts[3] = new Vector3(h + 1, d + lower, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + upper, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + upper, v);
                        break;

                    case Cluster.Block.Geometry.Fluid:
                        face.Verts[2] = new Vector3(h + 1, d, v);
                        face.Verts[3] = new Vector3(h + 1, d, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v);
                        break;

                    case Cluster.Block.Geometry.NorthFullRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1] };
                        break;

                    case Cluster.Block.Geometry.SouthFullRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = face.Verts[2];
                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1], face.UVs[0] };
                        break;

                    case Cluster.Block.Geometry.NorthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h + 1, d,v);
                        face.Verts[1] = new Vector3(h + 1, d, v+ 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1] };
                        break;

                    case Cluster.Block.Geometry.SouthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];
                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1], face.UVs[0] };
                        break;

                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + 0, v);
                        face.Verts[3] = new Vector3(h + 1, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 1f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        break;

                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + 0, v);
                        face.Verts[3] = new Vector3(h + 1, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        break;
                }

                return face;
            }

            private static Face BuildWestGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
            {
                return BuildWestGeometry(imageOffset, texture, h, v, d, block, TheWorld);
            }

            public static Face BuildWestGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block, World world)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture, world);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX * -1.0f;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Cluster.Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Cluster.Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Cluster.Block.Geometry.EastHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Cluster.Block.Geometry.Empty:
                    case Cluster.Block.Geometry.EastFullRamp:
                        return Face.Empty;

                    case Cluster.Block.Geometry.HalfLower:
                    case Cluster.Block.Geometry.HalfUpper:
                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.WestFullRamp:
                    case Cluster.Block.Geometry.WestHalfLowerRamp:
                    case Cluster.Block.Geometry.EastHalfUpperRamp:
                    case Cluster.Block.Geometry.WestHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + lower, v);
                        face.Verts[2] = new Vector3(h, d + upper, v);
                        face.Verts[3] = new Vector3(h, d + upper, v + 1);
                        face.Verts[0] = new Vector3(h, d + lower, v + 1);
                        break;

                    case Cluster.Block.Geometry.Fluid:
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.95f, v);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v + 1);
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        break;

                    case Cluster.Block.Geometry.NorthFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 1, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.SouthFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h, d + 1, v);
                        face.Verts[2] = new Vector3(h, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.NorthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.SouthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + 0, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0 ,v + 1);
                        break;

                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + 0, v);
                        face.Verts[2] = new Vector3(h, d + 1, v);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0, v + 1);
                        break;
                }

                return face;
            }

            private static Cluster.Block GetBlockNorthRelative(Cluster thisBlock, Cluster northBlock, int h, int v, int d)
            {
                v = v + 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (northBlock == null)
                    return Cluster.Block.Invalid;

                v = v - Cluster.HVSize;
                return northBlock.GetBlockRelative(h, v, d);
            }

            private static Cluster.Block GetBlockSouthRelative(Cluster thisBlock, Cluster southBlock, int h, int v, int d)
            {
                v = v - 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if(southBlock == null)
                    return Cluster.Block.Invalid;

                v = Cluster.HVSize + v;
                return southBlock.GetBlockRelative(h, v, d);
            }

            private static Cluster.Block GetBlockEastRelative(Cluster thisBlock, Cluster eastBlock, int h, int v, int d)
            {
                h = h + 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (eastBlock == null)
                    return Cluster.Block.Invalid;

                h = h - Cluster.HVSize;
                return eastBlock.GetBlockRelative(h, v, d);
            }

            private static Cluster.Block GetBlockWestRelative(Cluster thisBlock, Cluster westBlock, int h, int v, int d)
            {
                h = h - 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (westBlock == null)
                    return Cluster.Block.Invalid;

                h = Cluster.HVSize + h;
                return westBlock.GetBlockRelative(h, v, d);
            }

            public static void BuildGeometryForCluster(Cluster cluster)
            {
                World world = TheWorld;

                ClusterGeometry geometry = new ClusterGeometry();
                geometry.ClusterOrigin = cluster.Origin;

                Cluster northCluster = TheWorld.NeighborCluster(cluster.Origin, 0, 1, 0);
                Cluster southCluster = TheWorld.NeighborCluster(cluster.Origin, 0, -1, 0);
                Cluster eastCluster = TheWorld.NeighborCluster(cluster.Origin, 1, 0, 0);
                Cluster westCluster = TheWorld.NeighborCluster(cluster.Origin, -1, 0, 0);

                cluster.DoForEachBlock((h, v, d, block)=>
                        {
                            if (block.DefID < 0 || block.DefID >= world.BlockDefs.Count)
                                return;

                            World.BlockDef def = world.BlockDefs[block.DefID];

                            int topTexture = def.Top;
                            int[] sideTexture = new int[4] { topTexture, topTexture, topTexture, topTexture };

                            int lastTexture = topTexture;

                            if (def.Sides != null)
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    if (def.Sides.Length > i && def.Sides[i] != World.BlockDef.EmptyID)
                                        lastTexture = def.Sides[i];
                                    sideTexture[i] = lastTexture;
                                }
                            }
                            int bottomTexture = topTexture;
                            if (def.Bottom != World.BlockDef.EmptyID)
                                bottomTexture = def.Bottom;

                            if (block.Geom != Cluster.Block.Geometry.Empty)
                            {
                                int blockWorldH = cluster.Origin.H + h;
                                int blockWorldV = cluster.Origin.V + v;

//                                 if (cluster.Origin.H < 0)
//                                     blockWorldH += 1;
// 
//                                 if (cluster.Origin.V < 0)
//                                     blockWorldV += 1;

                                // see what's around us
                                if (topTexture != World.BlockDef.EmptyID && AboveIsOpen(block, cluster.GetBlockRelative(h, v, d + 1)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(topTexture)).Add(BuildAboveGeometry(world.BlockTextureToTextureOffset(topTexture), world.BlockTextureToTextureID(topTexture), h, v, d, block));

                                if (d != 0 && bottomTexture != World.BlockDef.EmptyID && BellowIsOpen(block, cluster.GetBlockRelative( h, v, d - 1)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(bottomTexture)).Add(BuildBelowGeometry(world.BlockTextureToTextureOffset(bottomTexture), world.BlockTextureToTextureID(bottomTexture),  h, v, d, block));

                                if (NorthIsOpen(block, GetBlockNorthRelative(cluster, northCluster, h, v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[0])).Add(BuildNorthGeometry(world.BlockTextureToTextureOffset(sideTexture[0]), world.BlockTextureToTextureID(sideTexture[0]), h, v, d, block));

                                if (SouthIsOpen(block, GetBlockSouthRelative(cluster, southCluster, h, v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[1])).Add(BuildSouthGeometry(world.BlockTextureToTextureOffset(sideTexture[1]), world.BlockTextureToTextureID(sideTexture[1]), h, v, d, block));

                                if (EastIsOpen(block, GetBlockEastRelative(cluster, eastCluster, h, v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[2])).Add(BuildEastGeometry(world.BlockTextureToTextureOffset(sideTexture[2]), world.BlockTextureToTextureID(sideTexture[2]),  h, v, d, block));

                                if (WestIsOpen(block, GetBlockWestRelative(cluster, westCluster, h, v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[3])).Add(BuildWestGeometry(world.BlockTextureToTextureOffset(sideTexture[3]), world.BlockTextureToTextureID(sideTexture[3]), h, v, d, block));
                            }
                    });

                geometry.FinalizeGeo();
                cluster.UpdateGeo(geometry);
            }

            public static void DoBuildGeometry()
            {
                foreach (Cluster cluster in TheWorld.Clusters.Values)
                    BuildGeometryForCluster(cluster);
            }
        }
    }
}
