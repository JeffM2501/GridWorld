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

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                return otherGeo.Trasperant || otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible || (otherGeo.Geom == Block.Geometries.Solid && otherGeo.MinHeight > 0);
            }

            private static bool IsLowerRamp(Block thisGeo)
            {
                if (thisGeo.Geom != Block.Geometries.Ramp)
                    return false;

                return thisGeo.MaxHeight < 1 && thisGeo.MinHeight == 0;
      }

            private static bool IsUpperRamp(Block thisGeo)
            {
                if (thisGeo.Geom != Block.Geometries.Ramp)
                    return false;

                return thisGeo.MinHeight > 0;
            }

            private static bool BellowIsOpen(Block thisGeo, Block otherGeo)
            {
                if (thisGeo.GetMinD() > 0)
                    return true;

                if (otherGeo == Block.Invalid || otherGeo.Geom == Block.Geometries.Solid)
                    return false;

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                if (otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible)
                    return true;

                return otherGeo.MaxHeight < 1;
            }

            private static Block.Directions GetOppositeDir(Block.Directions dir)
            {
                switch(dir)
                {
                    default:
                        return Block.Directions.None;

                    case Block.Directions.North:
                        return Block.Directions.South;

                    case Block.Directions.South:
                        return Block.Directions.North;

                    case Block.Directions.East:
                        return Block.Directions.West;

                    case Block.Directions.West:
                        return Block.Directions.East;
                }
            }

            private static bool DirectionIsOpen(Block thisGeo, Block otherGeo, Block.Directions dir)
            {
                if (otherGeo == Block.Invalid)
                    return false;

                if (thisGeo.Trasperant && otherGeo.Trasperant)
                    return false;

                if (otherGeo.Geom == Block.Geometries.Empty || otherGeo.Geom == Block.Geometries.Invisible)
                    return true;

                if (otherGeo.MinHeight > thisGeo.MinHeight || otherGeo.MaxHeight < thisGeo.MaxHeight)
                    return true; // there is some kind of gap, no mater the shape, so draw our side

                if (otherGeo.Geom == Block.Geometries.Solid && otherGeo.MaxHeight >= thisGeo.MaxHeight) // it is more solid than us
                     return false;

                if (otherGeo.Geom == Block.Geometries.Ramp && otherGeo.Dir == GetOppositeDir(dir) && otherGeo.MaxHeight >= thisGeo.MaxHeight)
                    return false; // is it the direct opposite ramp

                return true; // always default to true, because then we make the geo and there is no gap
            }

            public static Vector2[] GetConstUVOffsets()
            {
                return new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            }

            public static Face BuildAboveGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };
                face.UVs = GetConstUVOffsets();

                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                        face.Normal = Vector3.UnitY;
                        face.Verts[0] = new Vector3(h, maxD, v);
                        face.Verts[1] = new Vector3(h + 1, maxD, v);
                        face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                        face.Verts[3] = new Vector3(h, maxD, v + 1);
                        break;

                    case Block.Geometries.Ramp:
                        Vector3 rampVec;

                        switch (block.Dir)
                        {
                            case Block.Directions.North:
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[3] = new Vector3(h, maxD, v + 1);
                                break;

                            case Block.Directions.South:
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[3] = new Vector3(h, minD, v + 1);
                                break;

                            case Block.Directions.East:
                                face.Verts[0] = new Vector3(h, minD, v);
                                face.Verts[1] = new Vector3(h + 1, maxD, v);
                                face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                face.Verts[3] = new Vector3(h, minD, v + 1);
                                break;

                            case Block.Directions.West:
                                face.Verts[0] = new Vector3(h, maxD, v);
                                face.Verts[1] = new Vector3(h + 1, minD, v);
                                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                face.Verts[3] = new Vector3(h, maxD, v + 1);
                                break;
                        }

                        face.Normal = Vector3.UnitY;
                        break;
                }

                face.Normal.Normalize();

                return face;
            }

            public static Face BuildBelowGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                if (block.Geom == Block.Geometries.Empty || block.Geom == Block.Geometries.Invisible)
                    return Face.Empty;

                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                face.UVs = GetConstUVOffsets();
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitY * -1.0f;

                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                face.Verts[0] = new Vector3(h, minD, v);
                face.Verts[1] = new Vector3(h, minD, v + 1);
                face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                face.Verts[3] = new Vector3(h + 1, minD, v);

                return face;
            }

            private static float RampCenterUOffset = 0.017f; //015625f;

            public static Face BuildNorthGeometry(int imageOffset, int texture, Int64 h, Int64 v, Int64 d, Block block)
            {
                Face face = new Face();

                face.Verts = new Vector3[4] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ;

                float maxD = d + block.GetMaxD();
                float minD = d + block.GetMinD();
                float delta = block.GetMaxD() - block.GetMinD();

                face.UVs = new Vector2[4] { new Vector2(0, 1.0f - delta), new Vector2(1, 1.0f - delta), new Vector2(1, 1), new Vector2(0, 1) };

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.Ramp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Block.Directions.North)
                        {
                            face.Verts[3] = new Vector3(h, minD, v + 1);
                            face.Verts[0] = new Vector3(h, maxD, v + 1);
                            face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                            face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                        }
                        else if (block.Dir != Block.Directions.South)
                        {
                            switch (block.Dir)
                            {
                                case Block.Directions.East:
                                case Block.Directions.NorthEast:
                                    face.Verts[0] = new Vector3(h, minD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Block.Directions.West:
                                case Block.Directions.NorthWest:
                                    face.Verts[0] = new Vector3(h, d, v + 1);
                                    face.Verts[1] = new Vector3(h, d + 1, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;
                            }
                        }
                        else
                            return Face.Empty;
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

                face.UVs = new Vector2[4] { new Vector2(0, 1.0f - delta), new Vector2(1, 1.0f - delta), new Vector2(1, 1), new Vector2(0, 1) };
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitZ * -1;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.Ramp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Block.Directions.South)
                        {
                            face.Verts[0] = new Vector3(h, minD, v);
                            face.Verts[1] = new Vector3(h + 1, minD, v);
                            face.Verts[2] = new Vector3(h + 1, maxD, v);
                            face.Verts[3] = new Vector3(h, maxD, v);
                        }
                        else if (block.Dir != Block.Directions.North)
                        {
                            switch (block.Dir)
                            {
                                case Block.Directions.West:
                                case Block.Directions.SouthWest:
                                    face.Verts[0] = new Vector3(h, minD, v + 1);
                                    face.Verts[1] = new Vector3(h + 1, maxD, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Block.Directions.East:
                                case Block.Directions.SouthEast:
                                    face.Verts[0] = new Vector3(h, d, v + 1);
                                    face.Verts[1] = new Vector3(h, d + 1, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, d, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;
                            }
                        }
                        else
                            return Face.Empty;
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

                face.UVs = new Vector2[4] { new Vector2(0, 1.0f - delta), new Vector2(1, 1.0f - delta), new Vector2(1, 1), new Vector2(0, 1) };
                //Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.Ramp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Block.Directions.East)
                        {
                            face.Verts[2] = new Vector3(h + 1, minD, v);
                            face.Verts[3] = new Vector3(h + 1, minD, v + 1);
                            face.Verts[0] = new Vector3(h + 1, maxD, v + 1);
                            face.Verts[1] = new Vector3(h + 1, maxD, v);
                        }
                        else if (block.Dir != Block.Directions.West)
                        {
                            switch (block.Dir)
                            {
                                case Block.Directions.North:
                                case Block.Directions.NorthEast:
                                    face.Verts[0] = new Vector3(h + 1, minD, v);
                                    face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, maxD, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Block.Directions.East:
                                case Block.Directions.SouthEast:
                                    face.Verts[0] = new Vector3(h + 1, minD, v);
                                    face.Verts[1] = new Vector3(h + 1, minD, v + 1);
                                    face.Verts[2] = new Vector3(h + 1, maxD, v);
                                    face.Verts[3] = face.Verts[2];
                                    break;
                            }
                        }
                        else
                            return Face.Empty;
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

                face.UVs = new Vector2[4] { new Vector2(0, 1.0f - delta), new Vector2(1, 1.0f - delta), new Vector2(1, 1), new Vector2(0, 1) };
                Array.Reverse(face.UVs);
                face.Normal = Vector3.UnitX * -1.0f;

                switch (block.Geom)
                {
                    case Block.Geometries.Empty:
                    case Block.Geometries.Invisible:
                        return Face.Empty;

                    case Block.Geometries.Solid:
                    case Block.Geometries.Ramp:
                        if (block.Geom == Block.Geometries.Solid || block.Dir == Block.Directions.West)
                        {
                            face.Verts[1] = new Vector3(h, minD, v);
                            face.Verts[2] = new Vector3(h, maxD, v);
                            face.Verts[3] = new Vector3(h, maxD, v + 1);
                            face.Verts[0] = new Vector3(h, minD, v + 1);
                        }
                        else if (block.Dir != Block.Directions.East)
                        {
                            switch (block.Dir)
                            {
                                case Block.Directions.North:
                                case Block.Directions.NorthWest:
                                    face.Verts[0] = new Vector3(h, d, v + 1);
                                    face.Verts[1] = new Vector3(h, d, v);
                                    face.Verts[2] = new Vector3(h, d + 1, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;

                                case Block.Directions.South:
                                case Block.Directions.SouthEast:
                                    face.Verts[0] = new Vector3(h, d, v);
                                    face.Verts[1] = new Vector3(h, d + 1, v);
                                    face.Verts[2] = new Vector3(h, d, v + 1);
                                    face.Verts[3] = face.Verts[2];
                                    break;
                            }
                        }
                        else
                            return Face.Empty;
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

                                if (DirectionIsOpen(block, GetBlockNorthRelative(cluster, northCluster, h, v, d),Block.Directions.North))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[0])).Add(BuildNorthGeometry(World.BlockTextureToTextureOffset(sideTexture[0]), World.BlockTextureToTextureID(sideTexture[0]), h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockSouthRelative(cluster, southCluster, h, v, d), Block.Directions.South))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[1])).Add(BuildSouthGeometry(World.BlockTextureToTextureOffset(sideTexture[1]), World.BlockTextureToTextureID(sideTexture[1]), h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockEastRelative(cluster, eastCluster, h, v, d), Block.Directions.East))
                                    geometry.GetMesh(World.BlockTextureToTextureID(sideTexture[2])).Add(BuildEastGeometry(World.BlockTextureToTextureOffset(sideTexture[2]), World.BlockTextureToTextureID(sideTexture[2]),  h, v, d, block));

                                if (DirectionIsOpen(block, GetBlockWestRelative(cluster, westCluster, h, v, d), Block.Directions.West))
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
