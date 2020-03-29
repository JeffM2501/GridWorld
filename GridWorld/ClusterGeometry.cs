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
        public Cluster.ClusterPos ClusterOrigin = Cluster.ClusterPos.Zero;

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
            public List<Face> Faces = new List<Face>();

            public MeshGroup() { }
            public MeshGroup(int ID) { TextureID = ID; }
            public void Add(Face face)
            {
                if (face != Face.Empty)
                    Faces.Add((face));
            }
        }

        [XmlIgnore]
        public Dictionary<int, MeshGroup> Meshes = new Dictionary<int, MeshGroup>();

        [XmlIgnore]
        public Dictionary<int, MeshGroup> TranspereantMeshes = new Dictionary<int, MeshGroup>();

        public List<MeshGroup> MeshList = new List<MeshGroup>();
        public List<MeshGroup> TransperantMeshList = new List<MeshGroup>();

        public void Rebind()
        {
            Meshes.Clear();
            TranspereantMeshes.Clear();

            foreach (MeshGroup m in MeshList)
                Meshes.Add(m.TextureID, m);

            foreach (MeshGroup m in TransperantMeshList)
                TranspereantMeshes.Add(m.TextureID, m);
        }

        public MeshGroup GetMesh(int id, bool transperant)
        {
            Dictionary<int, MeshGroup> meshes = Meshes;
            List<MeshGroup> meshList = MeshList;
            if (transperant)
            {
                meshes = TranspereantMeshes;
                meshList = TransperantMeshList;
            }

            if (!meshes.ContainsKey(id))
            {
                meshes.Add(id, new MeshGroup(id));
                meshList.Add(meshes[id]);
            }

            return meshes[id];
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

                geo.Rebind();
                return geo;
            }
            catch (System.Exception /*ex*/)
            {
            }

            return ClusterGeometry.Empty;
        }

        private Tuple<Urho.Geometry, int> GetMeshData(MeshGroup mesh)
        {
            VertexBuffer buffer = new VertexBuffer(Application.Current.Context);
            List<VertexBuffer.PositionNormalColorTexcoord> verts = new List<VertexBuffer.PositionNormalColorTexcoord>();

            IndexBuffer indexes = new IndexBuffer(Application.Current.Context);

            List<short> indexData = new List<short>();
            // add geo

            uint white = Urho.Color.White.ToUInt();

            Vector3 localOrigin = new Vector3(ClusterOrigin.H, 0, ClusterOrigin.V);


            short vIndex = 0;
            foreach (var face in mesh.Faces)
            {
                Vector3 normal = face.Normal;
                indexData.Add(vIndex); vIndex++;
                verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[2] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[2] });

                indexData.Add(vIndex); vIndex++;
                verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[1] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[1] });

                indexData.Add(vIndex); vIndex++;
                verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[0] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[0] });

                if (face.Verts.Length == 4)
                {
                    indexData.Add(vIndex); vIndex++;
                    if (face.UVs.Length == 3)
                        verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[3] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[2] });
                    else
                        verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[3] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[3] });

                    indexData.Add(vIndex); vIndex++;
                    verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[2] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[2] });

                    indexData.Add(vIndex); vIndex++;
                    verts.Add(new VertexBuffer.PositionNormalColorTexcoord() { Position = face.Verts[0] - localOrigin, Normal = normal, Color = white, TexCoord = face.UVs[0] });
                }
            }

            buffer.Shadowed = true;

            buffer.SetSize((uint)verts.Count, ElementMask.Position | ElementMask.Normal | ElementMask.Color | ElementMask.TexCoord1, false);
            buffer.SetData(verts.ToArray());

            indexes.Shadowed = true;
            indexes.SetSize((uint)indexData.Count, false);
            indexes.SetData(indexData.ToArray());

            Urho.Geometry geo = new Urho.Geometry();
            geo.SetVertexBuffer(0, buffer);
            geo.IndexBuffer = indexes;
            geo.SetDrawRange(PrimitiveType.TriangleList, 0, (uint)verts.Count);

            return new Tuple<Geometry, int>(geo, mesh.TextureID);
        }

        public List<Tuple<Urho.Geometry, int>> BindToUrhoGeo()
        {
            List<Tuple<Urho.Geometry, int>> geoList = new List<Tuple<Urho.Geometry, int>>();
            foreach (var mesh in MeshList)
                geoList.Add(GetMeshData(mesh));

            foreach (var mesh in TransperantMeshList)
                geoList.Add(GetMeshData(mesh));

            return geoList;
        }

        public static void BuildGeometry(World world)
        {
            GeometryBuilder geoBuilder = new GeometryBuilder(world);
            geoBuilder.DoBuildGeometry();
        }

        public static void BuildGeometry(World world, Cluster cluster)
        {
            GeometryBuilder geoBuilder = new GeometryBuilder(world);
            geoBuilder.BuildGeometryForCluster(cluster);
        }

        public class GeometryBuilder
        {
            public World TheWorld = null;

            public GeometryBuilder(World world)
            {
                TheWorld = world;
            }

            protected bool AboveIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                return otherGeo.Geom == Cluster.Block.Geometry.Fluid || otherGeo.Geom == Cluster.Block.Geometry.Empty || otherGeo.Geom == Cluster.Block.Geometry.HalfUpper;
            }

            protected static bool IsLowerRamp(Cluster.Block thisGeo)
            {
                return thisGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.EastHalfLowerRamp || thisGeo.Geom == Cluster.Block.Geometry.WestHalfLowerRamp;
            }

            protected static bool IsUpperRamp(Cluster.Block thisGeo)
            {
                return thisGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp || thisGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp;
            }

            protected bool BellowIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;
                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.HalfUpper && !IsLowerRamp(otherGeo) && !IsUpperRamp(otherGeo);
            }

            protected bool NorthIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.SouthFullRamp && otherGeo.Geom != Cluster.Block.Geometry.SouthHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.SouthHalfUpperRamp;
            }

            protected bool SouthIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.SouthHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.NorthHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.SouthHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.NorthHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.NorthFullRamp && otherGeo.Geom != Cluster.Block.Geometry.NorthHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.NorthHalfUpperRamp;
            }

            protected bool EastIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
                if (thisGeo.Geom == Cluster.Block.Geometry.Fluid && otherGeo.Geom == Cluster.Block.Geometry.Fluid)
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.EastHalfLowerRamp && (otherGeo.Geom == Cluster.Block.Geometry.WestHalfLowerRamp || otherGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp))
                    return false;

                if (thisGeo.Geom == Cluster.Block.Geometry.EastHalfUpperRamp && otherGeo.Geom == Cluster.Block.Geometry.WestHalfUpperRamp)
                    return false;

                return otherGeo.Geom != Cluster.Block.Geometry.Solid && otherGeo.Geom != Cluster.Block.Geometry.WestFullRamp && otherGeo.Geom != Cluster.Block.Geometry.WestHalfLowerRamp && otherGeo.Geom != Cluster.Block.Geometry.WestHalfUpperRamp;
            }

            protected bool WestIsOpen(Cluster.Block thisGeo, Cluster.Block otherGeo)
            {
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

            public Vector2[] GetUVsForOffset(int imageOffset, int texture)
            {
                return GetUVsForOffset(imageOffset, texture, TheWorld);
            }

            protected Face BuildAboveGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            protected Face BuildBelowGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            protected static float RampCenterUOffset = 0.017f; //015625f;

            protected Face BuildNorthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            protected Face BuildSouthGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            protected Face BuildEastGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            protected Face BuildWestGeometry(int imageOffset, int texture, int h, int v, int d, Cluster.Block block)
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

            public Face ComputeLights(World world, World.BlockDef blockDef, Face face)
            {
                if (face.Verts == null)
                    return face;
                // walk each vertex and collide it with the sun
                // for multi light add code to collide with every light in radius (build non graphic octree?) and add lumens;
                float offset = 0.01f;

//                 for (int i = 0; i < 4; i++)
//                 {
//                     Vector3 newVec = face.Verts[i] + (face.Normal * offset);
// 
//                     Vector3 SunVec = world.Info.SunPosition - newVec;
//                     SunVec.Normalize();
// 
//                     float dot = Vector3.Dot(face.Normal, SunVec);
//                     if (dot >= 0)
//                     {
//                         if (world.LineCollidesWithWorld(newVec, world.Info.SunPosition).Collided)
//                             face.Luminance[i] = world.Info.Ambient;
//                         else
//                             face.Luminance[i] = world.Info.SunLuminance;
//                     }
//                     else
//                         face.Luminance[i] = 1;// world.Info.Ambient;
//                 }

                return face;
            }

            public void BuildGeometryForCluster(Cluster cluster)
            {
                World world = TheWorld;

                ClusterGeometry geometry = new ClusterGeometry();
                geometry.ClusterOrigin = cluster.Origin;

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
                                // see what's around us
                                if (topTexture != World.BlockDef.EmptyID && AboveIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h, cluster.Origin.V + v, d + 1)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(topTexture), def.Transperant).Add(ComputeLights(world, def, BuildAboveGeometry(world.BlockTextureToTextureOffset(topTexture), world.BlockTextureToTextureID(topTexture), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));

                                if (d != 0 && bottomTexture != World.BlockDef.EmptyID && BellowIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h, cluster.Origin.V + v, d - 1)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(bottomTexture), def.Transperant).Add(ComputeLights(world, def, BuildBelowGeometry(world.BlockTextureToTextureOffset(bottomTexture), world.BlockTextureToTextureID(bottomTexture), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));

                                if (!world.PositionIsOffMap(cluster.Origin.H + h, cluster.Origin.V + v + 1, d) && sideTexture[0] != World.BlockDef.EmptyID && NorthIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h, cluster.Origin.V + v + 1, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[0]), def.Transperant).Add(ComputeLights(world, def, BuildNorthGeometry(world.BlockTextureToTextureOffset(sideTexture[0]), world.BlockTextureToTextureID(sideTexture[0]), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));

                                if (!world.PositionIsOffMap(cluster.Origin.H + h, cluster.Origin.V + v - 1, d) && sideTexture[1] != World.BlockDef.EmptyID && SouthIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h, cluster.Origin.V + v - 1, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[1]), def.Transperant).Add(ComputeLights(world, def, BuildSouthGeometry(world.BlockTextureToTextureOffset(sideTexture[1]), world.BlockTextureToTextureID(sideTexture[1]), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));

                                if (!world.PositionIsOffMap(cluster.Origin.H + h + 1, cluster.Origin.V + v, d) && sideTexture[2] != World.BlockDef.EmptyID && EastIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h + 1, cluster.Origin.V + v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[2]), def.Transperant).Add(ComputeLights(world, def, BuildEastGeometry(world.BlockTextureToTextureOffset(sideTexture[2]), world.BlockTextureToTextureID(sideTexture[2]), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));

                                if (!world.PositionIsOffMap(cluster.Origin.H + h - 1, cluster.Origin.V + v, d) && sideTexture[3] != World.BlockDef.EmptyID && WestIsOpen(block, world.BlockFromPosition(cluster.Origin.H + h - 1, cluster.Origin.V + v, d)))
                                    geometry.GetMesh(world.BlockTextureToTextureID(sideTexture[3]), def.Transperant).Add(ComputeLights(world, def, BuildWestGeometry(world.BlockTextureToTextureOffset(sideTexture[3]), world.BlockTextureToTextureID(sideTexture[3]), cluster.Origin.H + h, cluster.Origin.V + v, d, block)));
                            }
                    });
   
                cluster.Geometry = geometry;
            }

            public void DoBuildGeometry()
            {
                World world = TheWorld;

                foreach (Cluster cluster in world.Clusters.Values)
                    BuildGeometryForCluster(cluster);
            }
        }
    }
}
