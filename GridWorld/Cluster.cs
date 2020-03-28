using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Urho;

namespace GridWorld
{
    public class Cluster : IOctreeObject
    {
        public class Block
        {
            public enum Geometry
            {
                Empty,
                Solid,
                Fluid,
                NorthFullRamp,
                SouthFullRamp,
                EastFullRamp,
                WestFullRamp,
                HalfUpper,
                HalfLower,
                NorthHalfLowerRamp,
                SouthHalfLowerRamp,
                EastHalfLowerRamp,
                WestHalfLowerRamp,
                NorthHalfUpperRamp,
                SouthHalfUpperRamp,
                EastHalfUpperRamp,
                WestHalfUpperRamp,
            }

            public int DefID;

            public Geometry Geom;

            public static Block Empty = new Block(World.BlockDef.EmptyID, Geometry.Empty);
            public static Block Invalid = new Block(World.BlockDef.EmptyID, Geometry.Empty);

            public Block() { }

            public Block(int id, Geometry geo)
            {
                DefID = id;
                Geom = geo;
            }

            public float GetZForLocalPosition(float x, float y)
            {
                switch (Geom)
                {
                    case Cluster.Block.Geometry.Solid:
                    case Cluster.Block.Geometry.HalfUpper:
                        return 1;

                    case Cluster.Block.Geometry.HalfLower:
                        return 0.5f;

                    case Cluster.Block.Geometry.NorthFullRamp:
                        return y;

                    case Cluster.Block.Geometry.SouthFullRamp:
                        return 1.0f - y;

                    case Cluster.Block.Geometry.EastFullRamp:
                        return x;

                    case Cluster.Block.Geometry.WestFullRamp:
                        return 1.0f - x;

                    case Cluster.Block.Geometry.NorthHalfLowerRamp:
                        return y * 0.5f;

                    case Cluster.Block.Geometry.SouthHalfLowerRamp:
                        return (1.0f - y) * 0.5f;

                    case Cluster.Block.Geometry.EastHalfLowerRamp:
                        return x * 0.5f;

                    case Cluster.Block.Geometry.WestHalfLowerRamp:
                        return (1.0f - x) * 0.5f;

                    case Cluster.Block.Geometry.NorthHalfUpperRamp:
                        return (y * 0.5f) + 0.5f;

                    case Cluster.Block.Geometry.SouthHalfUpperRamp:
                        return ((1.0f - y) * 0.5f) + 0.5f;

                    case Cluster.Block.Geometry.EastHalfUpperRamp:
                        return (x * 0.5f) + 0.5f;

                    case Cluster.Block.Geometry.WestHalfUpperRamp:
                        return ((1.0f - x) * 0.5f) + 0.5f;
                }

                return float.MinValue;
            }
        }

        public Block[] _Blocks = null;

        public Block[] Blocks
        {
            get
            {
                if (_Blocks == null)
                {
                    _Blocks = new Block[XYSize * XYSize * ZSize];
                    for (int i = 0; i < _Blocks.Length; i++)
                        _Blocks[i] = Block.Empty;
                }
                return _Blocks;
            }
        }

        public void ClearAllBlocks()
        {
            for (int i = 0; i < Blocks.Length; i++)
                Blocks[i] = Block.Empty;
            Geometry = null;
        }

        public Block GetBlockRelative(int x, int y, int z)
        {
            return Blocks[(z * XYSize * XYSize) + (y * XYSize) + x];
        }

        public Block GetBlockAbs(int x, int y, int z)
        {
            return GetBlockRelative(x - Origin.H, y - Origin.V, z);
        }

        public void SetBlockRelative(int x, int y, int z, Block block)
        {
            Blocks[(z * XYSize * XYSize) + (y * XYSize) + x] = block;
        }

        public void SetBlockAbs(int x, int y, int z, Block block)
        {
            SetBlockRelative(x - Origin.H, y - Origin.V, z, block);
        }

        public ClusterPos GetPositionRelative(Vector3 vec)
        {
            return new ClusterPos((int)vec.X - Origin.H, (int)vec.Y - Origin.V);
        }

        public Vector3 GetBlockRelativePostion(int index)
        {
            int z = index % (XYSize * XYSize);
            int planeStart = index - (z * (XYSize * XYSize));
            int y = planeStart % XYSize;
            int x = index - (y * XYSize);

            return new Vector3(x, y, z);
        }

        public Vector3 GetBlockRelativePostion(Block block)
        {
            return GetBlockRelativePostion(Array.IndexOf(Blocks, block));
        }

        public static int XYSize = 32;
        public static int ZSize = 32;

        public class ClusterPos
        {
            public static ClusterPos Zero = new ClusterPos(0, 0);

            public int H = 0;
            public int V = 0;

            public ClusterPos() { }
            public ClusterPos(int h, int v) { H = h; V = v; }
            public ClusterPos(ClusterPos pos) { H = pos.H; V = pos.V; }

            public override int GetHashCode()
            {
                return H.GetHashCode() ^ V.GetHashCode();
            }

            public override string ToString()
            {
                return H.ToString() + "," + V.ToString();
            }

            public override bool Equals(object obj)
            {
                ClusterPos p = obj as ClusterPos;
                if (p == null)
                    return false;

                return p.H == H && p.V == V;
            }
        }

        public ClusterPos Origin = ClusterPos.Zero;
        private bool BoundsValid = false;
        private BoundingBox _Bounds = new BoundingBox(0,0);

        public BoundingBox Bounds
        {
            get
            {
                if (!BoundsValid)
                {
                    BoundsValid = true;
                    _Bounds = new BoundingBox(new Vector3(Origin.H, Origin.V, 0), new Vector3(Origin.H + XYSize, Origin.V + XYSize, ZSize));
                }

                return _Bounds;
            }
        }

        public BoundingBox GetOctreeBounds()
        {
            return Bounds;
        }

        public delegate void BlockCallback(int x, int y, int z, Block block);

        public void DoForEachBlock(BlockCallback callback)
        {
            if (callback == null)
                return;

            for (int z = 0; z < ZSize; z++)
            {
                for (int y = 0; y < XYSize; y++)
                {
                    for (int x = 0; x < Cluster.XYSize; x++)
                        callback.Invoke(x, y, z, GetBlockRelative(x, y, z));
                }
            }
        }

        [XmlIgnore]
        public object Tag = null;

        [XmlIgnore]
        public object RenderTag = null;

        [XmlIgnore]
        public ClusterGeometry Geometry = null;
    }
}
