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

        public static void BuildGeometry()
        {
            GeometryBuilder.DoBuildGeometry();
        }

        public static void BuildGeometry(Cluster cluster)
        {
            GeometryBuilder.BuildGeometryForCluster(cluster);
        }

        public static class GeometryBuilder
        {
            private static bool AboveIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;

                return otherGeo.Geom == Block.Geometry.Fluid || otherGeo.Geom == Block.Geometry.Empty || otherGeo.Geom == Block.Geometry.HalfUpper;
            }

            private static bool IsLowerRamp(Block thisGeo)
            {
                return thisGeo.Geom == Block.Geometry.NorthHalfLowerRamp || thisGeo.Geom == Block.Geometry.SouthHalfLowerRamp || thisGeo.Geom == Block.Geometry.EastHalfLowerRamp || thisGeo.Geom == Block.Geometry.WestHalfLowerRamp;
            }

            private static bool IsUpperRamp(Block thisGeo)
            {
                return thisGeo.Geom == Block.Geometry.NorthHalfUpperRamp || thisGeo.Geom == Block.Geometry.SouthHalfUpperRamp || thisGeo.Geom == Block.Geometry.EastHalfUpperRamp || thisGeo.Geom == Block.Geometry.WestHalfUpperRamp;
            }

            private static bool BellowIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;


                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;
                return otherGeo.Geom != Block.Geometry.Solid && otherGeo.Geom != Block.Geometry.HalfUpper && !IsLowerRamp(otherGeo) && !IsUpperRamp(otherGeo);
            }

            private static bool NorthIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.NorthHalfLowerRamp && (otherGeo.Geom == Block.Geometry.SouthHalfLowerRamp || otherGeo.Geom == Block.Geometry.SouthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Block.Geometry.NorthHalfUpperRamp && otherGeo.Geom == Block.Geometry.SouthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Block.Geometry.Solid && otherGeo.Geom != Block.Geometry.SouthFullRamp && otherGeo.Geom != Block.Geometry.SouthHalfLowerRamp && otherGeo.Geom != Block.Geometry.SouthHalfUpperRamp;
            }

            private static bool SouthIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;


                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.SouthHalfLowerRamp && (otherGeo.Geom == Block.Geometry.NorthHalfLowerRamp || otherGeo.Geom == Block.Geometry.NorthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Block.Geometry.SouthHalfUpperRamp && otherGeo.Geom == Block.Geometry.NorthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Block.Geometry.Solid && otherGeo.Geom != Block.Geometry.NorthFullRamp && otherGeo.Geom != Block.Geometry.NorthHalfLowerRamp && otherGeo.Geom != Block.Geometry.NorthHalfUpperRamp;
            }

            private static bool EastIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.EastHalfLowerRamp && (otherGeo.Geom == Block.Geometry.WestHalfLowerRamp || otherGeo.Geom == Block.Geometry.WestHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Block.Geometry.EastHalfUpperRamp && otherGeo.Geom == Block.Geometry.WestHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Block.Geometry.Solid && otherGeo.Geom != Block.Geometry.WestFullRamp && otherGeo.Geom != Block.Geometry.WestHalfLowerRamp && otherGeo.Geom != Block.Geometry.WestHalfUpperRamp;
            }

            private static bool WestIsOpen(Block thisGeo, Block otherGeo)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.Fluid && otherGeo.Geom == Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Block.Geometry.WestHalfLowerRamp && (otherGeo.Geom == Block.Geometry.EastHalfLowerRamp || otherGeo.Geom == Block.Geometry.EastHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Block.Geometry.WestHalfUpperRamp && otherGeo.Geom == Block.Geometry.EastHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Block.Geometry.Solid && otherGeo.Geom != Block.Geometry.EastFullRamp && otherGeo.Geom != Block.Geometry.EastHalfLowerRamp && otherGeo.Geom != Block.Geometry.EastHalfUpperRamp;
            }

            public static Vector2[] GetUVsForOffset(int imageOffset, int texture)
            {
                World.TextureInfo info = World.Info.Textures[texture];

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

            public static Face BuildAboveGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                face.UVs = GetUVsForOffset(imageOffset, texture);

                switch (block.Geom)
                {
                    case Block.Geometry.Empty:
                        return Face.Empty;

                    case Block.Geometry.Solid:
                    case Block.Geometry.HalfUpper:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Block.Geometry.HalfLower:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Block.Geometry.Fluid:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, d + 0.95f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v + 1);
                        break;

                    case Block.Geometry.NorthFullRamp:
                        face.Normal = new Vector3(0, 1, -1);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Block.Geometry.SouthFullRamp:
                        face.Normal = new Vector3(0, 1, 1);

                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;

                    case Block.Geometry.EastFullRamp:
                        face.Normal = new Vector3(-1, 1, 0);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d+ 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;


                    case Block.Geometry.WestFullRamp:
                        face.Normal = new Vector3(1, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 1, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        break;

                    case Block.Geometry.NorthHalfLowerRamp:
                        face.Normal = new Vector3(0, 1, -0.5f);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Block.Geometry.SouthHalfLowerRamp:
                        face.Normal = new Vector3(0, 1, 0.5f);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;

                    case Block.Geometry.EastHalfLowerRamp:
                        face.Normal = new Vector3(-0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        break;


                    case Block.Geometry.WestHalfLowerRamp:
                        face.Normal = new Vector3(0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Block.Geometry.NorthHalfUpperRamp:
                        face.Normal = new Vector3(0, 1, -0.5f);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = new Vector3(h, d + 1f, v + 1);
                        break;

                    case Block.Geometry.SouthHalfUpperRamp:
                        face.Normal = new Vector3(0, 1, 0.5f);

                        face.Verts[0] = new Vector3(h, d + 1f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1f, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;

                    case Block.Geometry.EastHalfUpperRamp:
                        face.Normal = new Vector3(-0.5f, 1, 0);

                        face.Verts[0] = new Vector3(h, d + 0.5f, v);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1f, v + 1);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        break;


                    case Block.Geometry.WestHalfUpperRamp:
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

            public static Face BuildBelowGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                if (block.Geom == Block.Geometry.Empty)
                    return Face.Empty;

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitY * -1.0f;

                float ZOffset = 0;
                if (block.Geom == Block.Geometry.HalfUpper)
                    ZOffset = 0.5f;

                face.Verts[0] = new Vector3(h, d + ZOffset, v);
                face.Verts[1] = new Vector3(h, d + ZOffset, v + 1);
                face.Verts[2] = new Vector3(h + 1, d + ZOffset, v + 1);
                face.Verts[3] = new Vector3(h + 1, d + ZOffset, v);

                return face;
            }

            private static float RampCenterUOffset = 0.017f; //015625f;

            public static Face BuildNorthGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture);
                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Block.Geometry.SouthHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Block.Geometry.Empty:
                    case Block.Geometry.SouthFullRamp:
                        return Face.Empty;

                    case Block.Geometry.HalfUpper:
                    case Block.Geometry.HalfLower:
                    case Block.Geometry.Solid:
                    case Block.Geometry.NorthFullRamp:
                    case Block.Geometry.NorthHalfLowerRamp:
                    case Block.Geometry.NorthHalfUpperRamp:
                    case Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[3] = new Vector3(h, d + lower, v + 1);
                        face.Verts[0] = new Vector3(h, d + upper, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + upper, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + lower, v + 1);
                        break;

                    case Block.Geometry.Fluid:
                        face.Verts[3] = new Vector3(h, d, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0.95f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        break;

                    case Block.Geometry.EastFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[1], face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset) };
                        break;

                    case Block.Geometry.WestFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1], face.UVs[0] };
                        break;

                    case Block.Geometry.EastHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[1], face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f) };
                        break;

                    case Block.Geometry.WestHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1], face.UVs[0] };
                        break;

                    case Block.Geometry.EastHalfUpperRamp:

                        face.Verts[3] = new Vector3(h, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0, v + 1);

                        break;

                    case Block.Geometry.WestHalfUpperRamp:

                        face.Verts[3] = new Vector3(h, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h, d + 1, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0, v + 1);
                        break;
                }

                return face;
            }

            public static Face BuildSouthGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ * -1;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Block.Geometry.NorthHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Block.Geometry.Empty:
                    case Block.Geometry.NorthFullRamp:
                        return Face.Empty;

                    case Block.Geometry.HalfLower:
                    case Block.Geometry.HalfUpper:
                    case Block.Geometry.Solid:
                    case Block.Geometry.SouthFullRamp:
                    case Block.Geometry.SouthHalfLowerRamp:
                    case Block.Geometry.NorthHalfUpperRamp:
                    case Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + lower, v);
                        face.Verts[1] = new Vector3(h + 1, d + lower,v);
                        face.Verts[2] = new Vector3(h + 1, d + upper, v);
                        face.Verts[3] = new Vector3(h, d + upper, v);
                        break;

                    case Block.Geometry.Fluid:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.95f, v);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v);
                        break;

                    case Block.Geometry.EastFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2] };
                        break;

                    case Block.Geometry.WestFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h, d + 1, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.EastHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2] };
                        break;

                    case Block.Geometry.WestHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.EastHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + 0, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0, v);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v);
                        break;

                    case Block.Geometry.WestHalfUpperRamp:
                        face.Verts[0] = new Vector3(h, d + 0, v);
                        face.Verts[1] = new Vector3(h + 1, d + 0, v);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = new Vector3(h, d + 1, v);
                        break;
                }

                return face;
            }

            public static Face BuildEastGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture);
                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Block.Geometry.WestHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Block.Geometry.Empty:
                    case Block.Geometry.WestFullRamp:
                        return Face.Empty;

                    case Block.Geometry.HalfLower:
                    case Block.Geometry.HalfUpper:
                    case Block.Geometry.Solid:
                    case Block.Geometry.EastFullRamp:
                    case Block.Geometry.EastHalfLowerRamp:
                    case Block.Geometry.EastHalfUpperRamp:
                    case Block.Geometry.WestHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + lower, v);
                        face.Verts[3] = new Vector3(h + 1, d + lower, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + upper, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + upper, v);
                        break;

                    case Block.Geometry.Fluid:
                        face.Verts[2] = new Vector3(h + 1, d, v);
                        face.Verts[3] = new Vector3(h + 1, d, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 0.95f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.95f, v);
                        break;

                    case Block.Geometry.NorthFullRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1] };
                        break;

                    case Block.Geometry.SouthFullRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 1, v);
                        face.Verts[3] = face.Verts[2];
                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[1], face.UVs[0] };
                        break;

                    case Block.Geometry.NorthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h + 1, d,v);
                        face.Verts[1] = new Vector3(h + 1, d, v+ 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[0], face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1] };
                        break;

                    case Block.Geometry.SouthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h + 1, d, v);
                        face.Verts[1] = new Vector3(h + 1, d, v + 1);
                        face.Verts[2] = new Vector3(h + 1, d + 0.5f, v);
                        face.Verts[3] = face.Verts[2];
                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[1], face.UVs[0] };
                        break;

                    case Block.Geometry.NorthHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + 0, v);
                        face.Verts[3] = new Vector3(h + 1, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 1f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 0.5f, v);
                        break;

                    case Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[2] = new Vector3(h + 1, d + 0, v);
                        face.Verts[3] = new Vector3(h + 1, d + 0, v + 1);
                        face.Verts[0] = new Vector3(h + 1, d + 0.5f, v + 1);
                        face.Verts[1] = new Vector3(h + 1, d + 1, v);
                        break;
                }

                return face;
            }

            public static Face BuildWestGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetUVsForOffset(imageOffset, texture);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX * -1.0f;

                float lower = 0;
                float upper = 1;
                if (block.Geom == Block.Geometry.HalfUpper)
                    lower = 0.5f;
                else if (block.Geom == Block.Geometry.HalfLower || IsLowerRamp(block) || block.Geom == Block.Geometry.EastHalfUpperRamp)
                    upper = 0.5f;

                switch (block.Geom)
                {
                    case Block.Geometry.Empty:
                    case Block.Geometry.EastFullRamp:
                        return Face.Empty;

                    case Block.Geometry.HalfLower:
                    case Block.Geometry.HalfUpper:
                    case Block.Geometry.Solid:
                    case Block.Geometry.WestFullRamp:
                    case Block.Geometry.WestHalfLowerRamp:
                    case Block.Geometry.EastHalfUpperRamp:
                    case Block.Geometry.WestHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + lower, v);
                        face.Verts[2] = new Vector3(h, d + upper, v);
                        face.Verts[3] = new Vector3(h, d + upper, v + 1);
                        face.Verts[0] = new Vector3(h, d + lower, v + 1);
                        break;

                    case Block.Geometry.Fluid:
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.95f, v);
                        face.Verts[3] = new Vector3(h, d + 0.95f, v + 1);
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        break;

                    case Block.Geometry.NorthFullRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 1, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.SouthFullRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h, d + 1, v);
                        face.Verts[2] = new Vector3(h, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.NorthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v + 1);
                        face.Verts[1] = new Vector3(h, d, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.SouthHalfLowerRamp:
                        face.Verts[0] = new Vector3(h, d, v);
                        face.Verts[1] = new Vector3(h, d + 0.5f, v);
                        face.Verts[2] = new Vector3(h, d, v + 1);
                        face.Verts[3] = face.Verts[2];

                        face.UVs = new Vector2[3] { face.UVs[3] + ((face.UVs[1] - face.UVs[3]) * 0.5f) + new Vector2(0, RampCenterUOffset * 0.5f), face.UVs[2], face.UVs[3] };
                        break;

                    case Block.Geometry.NorthHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + 0, v);
                        face.Verts[2] = new Vector3(h, d + 0.5f, v);
                        face.Verts[3] = new Vector3(h, d + 1, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0 ,v + 1);
                        break;

                    case Block.Geometry.SouthHalfUpperRamp:
                        face.Verts[1] = new Vector3(h, d + 0, v);
                        face.Verts[2] = new Vector3(h, d + 1, v);
                        face.Verts[3] = new Vector3(h, d + 0.5f, v + 1);
                        face.Verts[0] = new Vector3(h, d + 0, v + 1);
                        break;
                }

                return face;
            }

            private static Block GetBlockNorthRelative(Cluster thisBlock, Cluster northBlock, Int64 h, Int64 v, Int64 d)
            {
                v = v + 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (northBlock == null)
                    return Block.Invalid;

                v = v - Cluster.HVSize;
                return northBlock.GetBlockRelative(h, v, d);
            }

            private static Block GetBlockSouthRelative(Cluster thisBlock, Cluster southBlock, Int64 h, Int64 v, Int64 d)
            {
                v = v - 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if(southBlock == null)
                    return Block.Invalid;

                v = Cluster.HVSize + v;
                return southBlock.GetBlockRelative(h, v, d);
            }

            private static Block GetBlockEastRelative(Cluster thisBlock, Cluster eastBlock, Int64 h, Int64 v, Int64 d)
            {
                h = h + 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (eastBlock == null)
                    return Block.Invalid;

                h = h - Cluster.HVSize;
                return eastBlock.GetBlockRelative(h, v, d);
            }

            private static Block GetBlockWestRelative(Cluster thisBlock, Cluster westBlock, Int64 h, Int64 v, Int64 d)
            {
                h = h - 1;

                if (h >= 0 && h < Cluster.HVSize && v >= 0 && v < (Cluster.HVSize))
                    return thisBlock.GetBlockRelative(h, v, d);

                if (westBlock == null)
                    return Block.Invalid;

                h = Cluster.HVSize + h;
                return westBlock.GetBlockRelative(h, v, d);
            }

            public static void BuildGeometryForCluster(Cluster cluster)
            {
                ClusterGeometry geometry = new ClusterGeometry();
                geometry.ClusterOrigin = cluster.Origin;

                Cluster northCluster = World.NeighborCluster(cluster.Origin, 0, 1, 0);
                Cluster southCluster = World.NeighborCluster(cluster.Origin, 0, -1, 0);
                Cluster eastCluster = World.NeighborCluster(cluster.Origin, 1, 0, 0);
                Cluster westCluster = World.NeighborCluster(cluster.Origin, -1, 0, 0);

                cluster.DoForEachBlock((h, v, d, block)=>
                        {
                            if (block.DefID < 0 || block.DefID >= World.BlockDefs.Count)
                                return;

                            World.BlockDef def = World.BlockDefs[block.DefID];

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

                            if (block.Geom != Block.Geometry.Empty)
                            {
                                Int64 blockWorldH = cluster.Origin.H + h;
                                Int64 blockWorldV = cluster.Origin.V + v;

//                                 if (cluster.Origin.H < 0)
//                                     blockWorldH += 1;
// 
//                                 if (cluster.Origin.V < 0)
//                                     blockWorldV += 1;

                                // see what's around us
                                if (topTexture != World.BlockDef.EmptyID && AboveIsOpen(block, cluster.GetBlockRelative(h, v, d + 1)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(topTexture)).Add(BuildAboveGeometry(World.BlockTextureToTextureOffset(topTexture), World.BlockTextureToTextureID(topTexture), h, v, d, block));

                                if (d != 0 && bottomTexture != World.BlockDef.EmptyID && BellowIsOpen(block, cluster.GetBlockRelative( h, v, d - 1)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(bottomTexture)).Add(BuildBelowGeometry(World.BlockTextureToTextureOffset(bottomTexture), World.BlockTextureToTextureID(bottomTexture),  h, v, d, block));

                                if (NorthIsOpen(block, GetBlockNorthRelative(cluster, northCluster, h, v, d)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[0])).Add(BuildNorthGeometry(World.BlockTextureToTextureOffset(sideTexture[0]), World.BlockTextureToTextureID(sideTexture[0]), h, v, d, block));

                                if (SouthIsOpen(block, GetBlockSouthRelative(cluster, southCluster, h, v, d)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[1])).Add(BuildSouthGeometry(World.BlockTextureToTextureOffset(sideTexture[1]), World.BlockTextureToTextureID(sideTexture[1]), h, v, d, block));

                                if (EastIsOpen(block, GetBlockEastRelative(cluster, eastCluster, h, v, d)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[2])).Add(BuildEastGeometry(World.BlockTextureToTextureOffset(sideTexture[2]), World.BlockTextureToTextureID(sideTexture[2]),  h, v, d, block));

                                if (WestIsOpen(block, GetBlockWestRelative(cluster, westCluster, h, v, d)))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[3])).Add(BuildWestGeometry(World.BlockTextureToTextureOffset(sideTexture[3]), World.BlockTextureToTextureID(sideTexture[3]), h, v, d, block));
                            }
                    });

                geometry.FinalizeGeo();
                cluster.UpdateGeo(geometry);
            }

            public static void DoBuildGeometry()
            {
                foreach (Cluster cluster in World.Clusters.Values)
                    BuildGeometryForCluster(cluster);
            }
        }
    }
}
