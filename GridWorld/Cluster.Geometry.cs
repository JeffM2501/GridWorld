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

            public void Add(Face[] faces)
            {
                foreach (var face in faces)
                    Add(face);
            }

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

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                if (otherGeo.Geom == Block.Geometries.LowerRamp && thisGeo.Geom == Block.Geometries.Solid)
                {
                    if (thisGeo.MaxHeight < Block.FullHeight)
                        return true;

                    return false;
                }

                if (thisGeo.MaxHeight < Block.FullHeight ||  otherGeo.MinHeight > Block.ZeroHeight)
                    return true;

                return otherGeo.Trasperant || otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible || (otherGeo.Geom == Block.Geometries.Solid && otherGeo.MinHeight > 0);
            }

            private static bool BellowIsOpen(Block thisGeo, Block otherGeo)
            {
                if (thisGeo.Geom != Block.Geometries.LowerRamp && thisGeo.GetMinD() > 0)
                    return true;

                if (otherGeo == Block.Invalid || otherGeo.Geom == Block.Geometries.Solid)
                    return false;

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                if (otherGeo.MaxHeight < Block.FullHeight)
                    return true;

                if (otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible)
                    return true;

                return otherGeo.MaxHeight < 1;
            }

            private static Directions GetOppositeDir(Directions dir)
            {
                switch(dir)
                {
                    default:
                        return Directions.None;

                    case Directions.North:
                        return Directions.South;

                    case Directions.South:
                        return Directions.North;

                    case Directions.East:
                        return Directions.West;

                    case Directions.West:
                        return Directions.East;
                }
            }

            private static bool DirectionIsOpen(Block thisGeo, Block otherGeo, Directions dir)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                if (otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible)
                    return true;

//                 if (otherGeo.MinHeight > thisGeo.MinHeight || otherGeo.MaxHeight < thisGeo.MaxHeight)
//                     return true; // there is some kind of gap, no mater the shape, so draw our side

                if (otherGeo.Geom == Block.Geometries.Solid && otherGeo.MaxHeight >= thisGeo.MaxHeight) // it is more solid than us
                     return false;

                // cascading lower ramps
                if (thisGeo.Geom == Block.Geometries.LowerRamp && otherGeo.Geom == Block.Geometries.LowerRamp)
                {
                    switch(dir)
                    {
                        case Directions.North:
                            if (thisGeo.Dir == Directions.North)
                            {
                                if (otherGeo.Dir == Directions.North)
                                    return otherGeo.MinHeight != thisGeo.MaxHeight;
                                if (otherGeo.Dir == Directions.South)
                                    return otherGeo.MaxHeight != thisGeo.MaxHeight;
                            }
                            if (thisGeo.Dir == Directions.South)
                            {
                                if (otherGeo.Dir == Directions.South)
                                    return otherGeo.MaxHeight != thisGeo.MinHeight;
                                if (otherGeo.Dir == Directions.North)
                                    return otherGeo.MinHeight != thisGeo.MinHeight;
                            }
                            break;

                        case Directions.South:
                            if (thisGeo.Dir == Directions.South)
                            {
                                if (otherGeo.Dir == Directions.South)
                                    return otherGeo.MinHeight != thisGeo.MaxHeight;
                                if (otherGeo.Dir == Directions.North)
                                    return otherGeo.MaxHeight != thisGeo.MaxHeight;
                            }
                            if (thisGeo.Dir == Directions.North)
                            {
                                if (otherGeo.Dir == Directions.North)
                                    return otherGeo.MaxHeight != thisGeo.MinHeight;
                                if (otherGeo.Dir == Directions.South)
                                    return otherGeo.MinHeight != thisGeo.MinHeight;
                            }
                            break;

                        case Directions.East:
                            if (thisGeo.Dir == Directions.East)
                            {
                                if (otherGeo.Dir == Directions.East)
                                    return otherGeo.MinHeight != thisGeo.MaxHeight;
                                if (otherGeo.Dir == Directions.West)
                                    return otherGeo.MaxHeight != thisGeo.MaxHeight;
                            }
                            if (thisGeo.Dir == Directions.West)
                            {
                                if (otherGeo.Dir == Directions.West)
                                    return otherGeo.MaxHeight != thisGeo.MinHeight;
                                if (otherGeo.Dir == Directions.East)
                                    return otherGeo.MinHeight != thisGeo.MinHeight;
                            }
                            break;

                        case Directions.West:
                            if (thisGeo.Dir == Directions.West)
                            {
                                if (otherGeo.Dir == Directions.West)
                                    return otherGeo.MinHeight != thisGeo.MaxHeight;
                                if (otherGeo.Dir == Directions.East)
                                    return otherGeo.MaxHeight != thisGeo.MaxHeight;
                            }
                            if (thisGeo.Dir == Directions.East)
                            {
                                if (otherGeo.Dir == Directions.East)
                                    return otherGeo.MaxHeight != thisGeo.MinHeight;
                                if (otherGeo.Dir == Directions.West)
                                    return otherGeo.MinHeight != thisGeo.MinHeight;
                            }
                            break;
                    }
                   
                }

                // cascading lower to full ramp
                if (thisGeo.Geom == Block.Geometries.LowerRamp && otherGeo.Geom == Block.Geometries.FullRamp && otherGeo.MinHeight == Block.ZeroHeight)
                {
                    var opposite = GetOppositeDir(dir);
                    if (thisGeo.Dir == opposite && thisGeo.Dir == otherGeo.Dir && otherGeo.MaxHeight == thisGeo.MinHeight)
                        return false;
                }

                if (thisGeo.Geom == Block.Geometries.FullRamp && otherGeo.Geom == Block.Geometries.LowerRamp && thisGeo.MinHeight == Block.ZeroHeight)
                {
                    if (thisGeo.Dir == dir && thisGeo.Dir == otherGeo.Dir && thisGeo.MaxHeight == otherGeo.MinHeight)
                        return false;
                }

                // identical ramps
                if (thisGeo.MaxHeight == otherGeo.MaxHeight && thisGeo.MinHeight == otherGeo.MinHeight)
                {
                    if (thisGeo.Geom == Block.Geometries.Solid && otherGeo.Geom == Block.Geometries.Solid)
                        return false; // the full wall is shared between solid blocks.

                    // is ramps are solid only in one direction
                    if (thisGeo.Geom == Block.Geometries.Solid && otherGeo.Geom == Block.Geometries.FullRamp)
                        return otherGeo.Dir != GetOppositeDir(dir);   

                    if ((thisGeo.Geom == Block.Geometries.FullRamp && otherGeo.Geom == Block.Geometries.FullRamp) || (thisGeo.Geom == Block.Geometries.LowerRamp && otherGeo.Geom == Block.Geometries.LowerRamp))
                    {
                        switch (dir)
                        {
                            case Directions.North:
                                switch (thisGeo.Dir)
                                {
                                    case Directions.North:
                                        return otherGeo.Dir != Directions.South;

                                    case Directions.NorthEast:
                                    case Directions.East:
                                        return otherGeo.Dir != Directions.East && otherGeo.Dir != Directions.SouthEast;

                                    case Directions.NorthWest:
                                    case Directions.West:
                                        return otherGeo.Dir != Directions.West && otherGeo.Dir != Directions.SouthWest;
                                }
                                break;

                            case Directions.South:
                                switch (thisGeo.Dir)
                                {
                                    case Directions.South:
                                        return otherGeo.Dir != Directions.North;

                                    case Directions.SouthEast:
                                    case Directions.East:
                                        return otherGeo.Dir != Directions.East && otherGeo.Dir != Directions.NorthEast;

                                    case Directions.SouthWest:
                                    case Directions.West:
                                        return otherGeo.Dir != Directions.West && otherGeo.Dir != Directions.NorthWest;
                                }
                                break;

                            case Directions.East:
                                switch (thisGeo.Dir)
                                {
                                    case Directions.East:
                                        return otherGeo.Dir != Directions.West;

                                    case Directions.NorthEast:
                                    case Directions.North:
                                        return otherGeo.Dir != Directions.North && otherGeo.Dir != Directions.NorthWest;

                                    case Directions.SouthEast:
                                    case Directions.South:
                                        return otherGeo.Dir != Directions.South && otherGeo.Dir != Directions.SouthWest;
                                }
                                break;

                            case Directions.West:
                                switch (thisGeo.Dir)
                                {
                                    case Directions.West:
                                        return otherGeo.Dir != Directions.East;

                                    case Directions.SouthWest:
                                    case Directions.South:
                                        return otherGeo.Dir != Directions.South && otherGeo.Dir != Directions.SouthEast;

                                    case Directions.NorthWest:
                                    case Directions.North:
                                        return otherGeo.Dir != Directions.North && otherGeo.Dir != Directions.NorthEast;
                                }
                                break;

                            default:
                                return true;
                        }

                        return true;
                    }
                }

                return true; // always default to true, because then we make the geo and there is no gap
            }

            private static float SlopeVOffset(float delta)
            {
                return (1.0f - delta) * 0.5f;
            }

            public static Vector2[] GetConstUVOffsets( float delta )
            {
                return new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1 - SlopeVOffset(delta)), new Vector2(0, 1 - SlopeVOffset(delta)) };
            }

            public static Vector2[] GetConstUVOffsets(float nearDelta, float farDelta)
            {
                return new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1 - SlopeVOffset(farDelta)), new Vector2(0, 1 - SlopeVOffset(nearDelta)) };
            }

            public static void SetTopVectorForDir(ref Vector3 norm, Directions dir, float delta)
            {
                norm = Vector3.UnitY;
                switch (dir)
                {
                    default:
                        return;

                    case Directions.North:
                        norm = Vector3.Cross(Vector3.UnitX * -1, new Vector3(0, delta, 1));
                        break;

                    case Directions.South:
                        norm = Vector3.Cross(Vector3.UnitX, new Vector3(0, delta, -1));
                        break;

                    case Directions.East:
                        norm = Vector3.Cross(Vector3.UnitZ, new Vector3(1, delta, 0));
                        break;

                    case Directions.West:
                        norm = Vector3.Cross(Vector3.UnitZ * -1.0f, new Vector3(-1, delta, 0));
                        break;
                }

                norm.Normalize();
            }

            public static Face[] BuildAboveGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                List<Face> faces = new List<Face>();

                Face face = new Face();
                faces.Add(face);
                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                face.UVs = GetConstUVOffsets(0);

                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return new Face[0];

                    case Block.Geometries.Solid:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, maxD, v);
                        face.Verts[1] = new Vector3(h + 1, maxD, v);
                        face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                        face.Verts[3] = new Vector3(h, maxD, v + 1);
                        SetTopVectorForDir(ref face.Normal,Directions.None,0);
                        break;

                    case Block.Geometries.FullRamp:
                    case Block.Geometries.LowerRamp:
                        switch (block.Dir)
                        {
                            case Directions.North:
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[3] = new Vector3(h, maxD, v + 1);

                                SetTopVectorForDir(ref face.Normal, block.Dir, delta);
                                break;

                            case Directions.South:
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[3] = new Vector3(h, minD, v + 1);

                                SetTopVectorForDir(ref face.Normal, block.Dir, delta);
                                break;

                            case Directions.East:
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[3] = new Vector3(h, minD, v + 1);

                                SetTopVectorForDir(ref face.Normal, block.Dir, delta);
                                break;

                            case Directions.West:
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[3] = new Vector3(h, maxD, v + 1);

                                SetTopVectorForDir(ref face.Normal, block.Dir, delta);
                                break;

                            case Directions.NorthEast:
                                // south lower to north higher face
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[3] = face.Verts[2];
                                SetTopVectorForDir(ref face.Normal, Directions.North, delta);
  
                                // west lower to east higher
                                face = new Face();
                                faces.Add(face);
                                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                                face.UVs = new Vector2[4] { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 1) };

                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[2] = new Vector3(h, minD, v + 1);
                                face.Verts[3] = face.Verts[2];

                                SetTopVectorForDir(ref face.Normal, Directions.East, delta);
                                break;

                            case Directions.NorthWest:
                                // south lower to north higher face
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h, maxD, v+1);
                                face.Verts[3] = face.Verts[2];

                                face.UVs = new Vector2[4] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };

                                SetTopVectorForDir(ref face.Normal, Directions.North, delta);

                                // east lower to west higher face
                                face = new Face();
                                faces.Add(face);
                                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                                face.UVs = new Vector2[4] { new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1) };

                                face.Verts[0] = new Vector3(h+1, minD, v);
                                face.Verts[1] = new Vector3(h+1, minD, v + 1);
                                face.Verts[2] = new Vector3(h, maxD, v+1);
                                face.Verts[3] = face.Verts[2];
                                SetTopVectorForDir(ref face.Normal, Directions.West, delta);
                                break;

                            case Directions.SouthEast:
                                 // west lower to east higher face
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.Verts[2] = new Vector3(h, minD, v + 1);
                                face.Verts[3] = face.Verts[2];

                                face.UVs = new Vector2[4] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
                                SetTopVectorForDir(ref face.Normal, Directions.East, delta);

                                // north lower to south higher face
                                face = new Face();
                                faces.Add(face);
                                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                                face.UVs = new Vector2[4] { new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1) };

                                face.Verts[0] = new Vector3(h + 1, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[2] = new Vector3(h, minD, v + 1);
                                face.Verts[3] = face.Verts[2];

                                SetTopVectorForDir(ref face.Normal, Directions.South, delta);
                                break;

                            case Directions.SouthWest:
                                // north lower to south higher face
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v+1);
                                face.Verts[2] = new Vector3(h, minD, v + 1);
                                face.Verts[3] = face.Verts[2];

                                SetTopVectorForDir(ref face.Normal, Directions.South, delta);

                                // east lower to west higher face
                                face = new Face();
                                faces.Add(face);
                                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                                face.UVs = new Vector2[4] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };

                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[3] = face.Verts[2];

                                SetTopVectorForDir(ref face.Normal, Directions.West, delta);
                                break;

                        }
                        break;
                }

                return faces.ToArray();
            }

            public static Face BuildBelowGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                if (block.Geom == Block.Geometries.Empty || block.Geom == Block.Geometries.Invisible)
                    return Face.Empty;

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetConstUVOffsets(0);
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitY * -1.0f;

                float minD = d;

                if (block.Geom != Block.Geometries.LowerRamp)
                    minD = d + block.GetMinD();

                face.Verts[0] = new Vector3(h, minD, v);
                face.Verts[1] = new Vector3(h, minD, v + 1);
                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                face.Verts[3] = new Vector3(h + 1, minD, v);

                return face;
            }

            public static Face BuildNorthGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ;

                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                face.UVs = GetConstUVOffsets(delta);

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.FullRamp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Directions.North)
                        {
                            face.Verts[3] = new Vector3(h, minD, v + 1);
                            face.Verts[0] = new Vector3(h, maxD, v + 1);
                            face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                            face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                        }
                        else if (block.Dir != Directions.South)
                        {
                            switch (block.Dir)
                            {
                                case Directions.East:
                                case Directions.NorthEast:
                                    face.Verts[0] = new Vector3(h, minD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Directions.West:
                                case Directions.NorthWest:
                                    face.Verts[0] = new Vector3(h, maxD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[2] = new Vector3(h, minD, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                default:
                                    return Face.Empty;
                            }
                        }
                        else
                            return Face.Empty;
                        break;

                    case Block.Geometries.LowerRamp:
                        switch(block.Dir)
                        {
                            case Directions.North:
                                face.Verts[3] = new Vector3(h, d, v + 1);
                                face.Verts[0] = new Vector3(h, maxD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                face.UVs = GetConstUVOffsets(1);
                                break;

                            case Directions.South:
                            case Directions.SouthEast:
                            case Directions.SouthWest:
                                face.Verts[3] = new Vector3(h, d, v + 1);
                                face.Verts[0] = new Vector3(h, minD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                face.UVs = GetConstUVOffsets(1);
                                break;

                            case Directions.East:
                            case Directions.NorthEast:
                                face.Verts[3] = new Vector3(h, d, v + 1);
                                face.Verts[0] = new Vector3(h, minD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                face.UVs = GetConstUVOffsets(minD - d, maxD - d);
                                break;

                            case Directions.West:
                            case Directions.NorthWest:
                                face.Verts[3] = new Vector3(h, d, v + 1);
                                face.Verts[0] = new Vector3(h, maxD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                face.UVs = GetConstUVOffsets(maxD - d, minD - d);
                                break;   
                        }
                        break;
                }
                return face;
            }

            public static Face BuildSouthGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetConstUVOffsets(delta);

                face.Normal = Vector3.UnitZ * -1;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.FullRamp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Directions.South)
                        {
                            face.Verts[0] = new Vector3(h, minD, v);
                            face.Verts[1] = new Vector3(h + 1, minD, v);
                            face.Verts[2] = new Vector3(h + 1, maxD, v);
                            face.Verts[3] = new Vector3(h, maxD, v);
                            Array.Reverse(face.UVs);
                        }
                        else if (block.Dir != Directions.North)
                        {
                            switch (block.Dir)
                            {
                                case Directions.West:
                                case Directions.SouthWest:
                                    face.Verts[0] = new Vector3(h + 1, minD, v);
                                    face.Verts[1] = new Vector3(h, maxD, v);
                                    face.Verts[2] = new Vector3(h, minD, v);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Directions.East:
                                case Directions.SouthEast:
                                    face.Verts[0] = new Vector3(h + 1, maxD, v);
                                    face.Verts[1] = new Vector3(h, minD, v);
                                    face.Verts[2] = new Vector3(h + 1, minD, v);

                                    face.Verts[3] = face.Verts[2];
                                    break;

                                default:
                                    return Face.Empty;
                            }
                        }
                        else
                            return Face.Empty;
                        break;

                    case Block.Geometries.LowerRamp:
                        switch (block.Dir)
                        {
                            case Directions.North:
                            case Directions.NorthEast:
                            case Directions.NorthWest:
                                face.Verts[0] = new Vector3(h + 1, minD, v);
                                face.Verts[1] = new Vector3(h, minD, v);
                                face.Verts[2] = new Vector3(h, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v);
                                face.UVs = GetConstUVOffsets(minD-d);
                                break;

                            case Directions.South:
                                face.Verts[0] = new Vector3(h + 1, maxD, v);
                                face.Verts[1] = new Vector3(h, maxD, v);
                                face.Verts[2] = new Vector3(h, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v);
                                face.UVs = GetConstUVOffsets(1);
                                break;

                            case Directions.East:
                            case Directions.SouthEast:
                                face.Verts[0] = new Vector3(h + 1, maxD, v);
                                face.Verts[1] = new Vector3(h, minD, v);
                                face.Verts[2] = new Vector3(h, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v);
                                face.UVs = GetConstUVOffsets(maxD - d, minD - d);
                                break;

                            case Directions.West:
                            case Directions.SouthWest:
                                face.Verts[0] = new Vector3(h + 1, minD, v); 
                                face.Verts[1] = new Vector3(h, maxD, v);
                                face.Verts[2] = new Vector3(h, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v);
                                face.UVs = GetConstUVOffsets(minD - d, maxD - d);
                                break;
                        }
                        break;
                }

                return face;
            }

            public static Face BuildEastGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetConstUVOffsets(delta);
                
                face.Normal = Vector3.UnitX;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.FullRamp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Directions.East)
                        {
                            face.Verts[2] = new Vector3(h + 1, minD, v);
                            face.Verts[3] = new Vector3(h + 1, minD, v + 1);
                            face.Verts[0] = new Vector3(h + 1, maxD, v + 1);
                            face.Verts[1] = new Vector3(h + 1, maxD, v);
                        }
                        else if (block.Dir != Directions.West)
                        {
                            switch (block.Dir)
                            {
                                case Directions.North:
                                case Directions.NorthEast:
                                    face.Verts[0] = new Vector3(h + 1, maxD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, minD, v);
                                    face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[3] = face.Verts[2];
      
                                    break;

                                case Directions.South:
                                case Directions.SouthEast:
                                    face.Verts[0] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, maxD, v);
                                    face.Verts[2] = new Vector3(h + 1, minD, v);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                default:
                                    return Face.Empty;
                            }
                        }
                        else
                            return Face.Empty;
                        break;

                    case Block.Geometries.LowerRamp:
                        switch (block.Dir)
                        {
                            case Directions.North:
                            case Directions.NorthEast:
                                face.Verts[2] = new Vector3(h + 1, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v + 1);
                                face.Verts[0] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.UVs = GetConstUVOffsets(maxD - d, minD - d);
                                break;

                            case Directions.South:
                            case Directions.SouthEast:
                                face.Verts[2] = new Vector3(h + 1, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v + 1);
                                face.Verts[0] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.UVs = GetConstUVOffsets(minD - d, maxD - d);
                                break;

                            case Directions.East:
                                face.Verts[2] = new Vector3(h + 1, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v + 1);
                                face.Verts[0] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.UVs = GetConstUVOffsets(1);
                                break;

                            case Directions.West:
                            case Directions.SouthWest:
                            case Directions.NorthWest:
                                face.Verts[2] = new Vector3(h + 1, d, v);
                                face.Verts[3] = new Vector3(h + 1, d, v + 1);
                                face.Verts[0] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.UVs = GetConstUVOffsets(minD - d);
                                break;
                        }
                        break;
                }
                return face;
            }

            public static Face BuildWestGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetConstUVOffsets(delta);

                face.Normal = Vector3.UnitX * -1.0f;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.FullRamp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Directions.West)
                        {
                            face.Verts[1] = new Vector3(h, minD, v);
                            face.Verts[2] = new Vector3(h, maxD, v);
                            face.Verts[3] = new Vector3(h, maxD, v + 1);
                            face.Verts[0] = new Vector3(h, minD, v + 1);
                            Array.Reverse(face.UVs);
                        }
                        else if (block.Dir != Directions.East)
                        {
                            switch (block.Dir)
                            {
                                case Directions.North:
                                case Directions.NorthWest:
                                    face.Verts[0] = new Vector3(h, minD, v);
                                    face.Verts[1] = new Vector3(h, maxD, v + 1);
                                    face.Verts[2] = new Vector3(h, minD, v + 1);

                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Directions.South:
                                case Directions.SouthWest:
                                    face.Verts[0] = new Vector3(h, maxD, v); 
                                    face.Verts[1] = new Vector3(h, minD, v + 1);
                                    face.Verts[2] = new Vector3(h, minD, v);

                                    face.Verts[3] = face.Verts[2];
                                    break;
                                default:
                                    return Face.Empty;
                            }
                        }
                        else
                            return Face.Empty;
                        break;

                    case Block.Geometries.LowerRamp:
                        switch (block.Dir)
                        {
                            case Directions.North:
                            case Directions.NorthWest:
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h, maxD, v + 1);
                                face.Verts[2] = new Vector3(h, d, v + 1);
                                face.Verts[3] = new Vector3(h, d, v);
                                face.UVs = GetConstUVOffsets(minD - d, maxD - d);
                               
                                break;

                            case Directions.South:
                            case Directions.SouthWest:
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h, minD, v + 1);
                                face.Verts[2] = new Vector3(h, d, v + 1);
                                face.Verts[3] = new Vector3(h, d, v);
                                face.UVs = GetConstUVOffsets(maxD - d, minD - d);
                                break;

                            case Directions.East:
                            case Directions.NorthEast:
                            case Directions.SouthEast:
                                face.Verts[1] = new Vector3(h, d, v);
                                face.Verts[2] = new Vector3(h, minD, v);
                                face.Verts[3] = new Vector3(h, minD, v + 1);
                                face.Verts[0] = new Vector3(h, d, v + 1);
                                face.UVs = GetConstUVOffsets(minD-d);
                                Array.Reverse(face.UVs);
                                break;

                            case Directions.West:
                                face.Verts[1] = new Vector3(h, d, v);
                                face.Verts[2] = new Vector3(h, maxD, v);
                                face.Verts[3] = new Vector3(h, maxD, v + 1);
                                face.Verts[0] = new Vector3(h, d, v + 1);
                                face.UVs = GetConstUVOffsets(1);
                                Array.Reverse(face.UVs);
                                break;
                        }
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

                            if (block.Geom != Block.Geometries.Empty && block.Geom != Block.Geometries.Invisible)
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

                                if (DirectionIsOpen(block, GetBlockNorthRelative(cluster, northCluster, h, v, d),Directions.North))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[0])).Add(BuildNorthGeometry(World.BlockTextureToTextureOffset(sideTexture[0]), World.BlockTextureToTextureID(sideTexture[0]), h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockSouthRelative(cluster, southCluster, h, v, d), Directions.South))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[1])).Add(BuildSouthGeometry(World.BlockTextureToTextureOffset(sideTexture[1]), World.BlockTextureToTextureID(sideTexture[1]), h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockEastRelative(cluster, eastCluster, h, v, d), Directions.East))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[2])).Add(BuildEastGeometry(World.BlockTextureToTextureOffset(sideTexture[2]), World.BlockTextureToTextureID(sideTexture[2]),  h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockWestRelative(cluster, westCluster, h, v, d), Directions.West))
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
