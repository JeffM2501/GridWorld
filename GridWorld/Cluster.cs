using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Urho;

namespace GridWorld
{
    public partial class Cluster : EventArgs,  IOctreeObject
    {
        public ushort[] _Blocks = null;

        private ushort[] Blocks
        {
            get
            {
                if (_Blocks == null)
                {
                    _Blocks = new ushort[HVSize * HVSize * DSize];
                    for (int i = 0; i < _Blocks.Length; i++)
                        _Blocks[i] = 0;
                }
                return _Blocks;
            }
        }

        public void ClearAllBlocks()
        {
            for (int i = 0; i < Blocks.Length; i++)
                Blocks[i] = 0;
            Geometry = null;
        }

        public Block GetBlockRelative(int h, int v, int d)
        {
            try
            {
                return World.GetBlock(Blocks[(d * HVSize * HVSize) + (v * HVSize) + h]);
            }
            catch (Exception)
            {

                throw;
            }
           
        }

        public Block GetBlockAbs(int h, int v, int d)
        {
            return GetBlockRelative(h - Origin.H, v - Origin.V, d);
        }

        public void SetBlockRelative(int h, int v, int d, Block block)
        {
            Blocks[(d * HVSize * HVSize) + (v * HVSize) + h] = World.AddBlock(block);
        }

        public void SetBlockAbs(int h, int v, int d, Block block)
        {
            SetBlockRelative(h - Origin.H, v - Origin.V, d, block);
        }

        public ClusterPos GetPositionRelative(Vector3 vec)
        {
            return new ClusterPos((int)vec.X - Origin.H, (int)vec.Z - Origin.V);
        }

        public Vector3 GetBlockRelativePostion(int index)
        {
            int d = index % (HVSize * HVSize);
            int planeStart = index - (d * (HVSize * HVSize));
            int v = planeStart % HVSize;
            int h = index - (v * HVSize);

            return new Vector3(h, d, v);
        }

        public Vector3 GetBlockRelativePostion(Block block)
        {
            return GetBlockRelativePostion(Array.IndexOf(Blocks, block));
        }

        public const int HVSize = 32;
        public const int DSize = 32;

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
                    _Bounds = new BoundingBox(new Vector3(Origin.H, 0, Origin.V), new Vector3(Origin.H + HVSize, DSize, Origin.V + HVSize ));
                }

                return _Bounds;
            }
        }

        public BoundingBox GetOctreeBounds()
        {
            return Bounds;
        }

        public delegate void BlockCallback(int h, int v, int d, Block block);

        public void DoForEachBlock(BlockCallback callback)
        {
            if (callback == null)
                return;

            for (int d = 0; d < DSize; d++)
            {
                for (int v = 0; v < HVSize; v++)
                {
                    for (int h = 0; h < Cluster.HVSize; h++)
                        callback.Invoke(h, v, d, GetBlockRelative(h, v, d));
                }
            }
        }

        public delegate void BlockGeoCallback(Vector3 blockPos, Block block);

        public void DoForEachBlock(BlockGeoCallback callback)
        {
            if (callback == null)
                return;

            for (int d = 0; d < DSize; d++)
            {
                for (int v = 0; v < HVSize; v++)
                {
                    for (int h = 0; h < Cluster.HVSize; h++)
                        callback.Invoke(new Vector3(h,d,v), GetBlockRelative(h, v, d));
                }
            }
        }

        public enum Statuses
        {
            Raw,
            Generated,
            GeometryPending,
            GeometryCreated,
            GeometryBound,
        }

        [XmlIgnore]
        private Statuses Status = Statuses.Raw;

        private object StatusLocker = new object();

        public Statuses GetStatus()
        {
            lock (StatusLocker)
                return Status;
        }

        public void StartGeo()
        {
            lock (StatusLocker)
                Status = Statuses.GeometryCreated;
        }

        public void FinalizeGeneration()
        {
            lock (StatusLocker)
                Status = Statuses.Generated;
        }

        public void FinalizeBind()
        {
            lock (StatusLocker)
            {
                Status = Statuses.GeometryBound;
                NeedBinding = false;
            }
        }

        private bool NeedBinding = false;

        public void RequestBinding()
        {
            lock (StatusLocker)
            {
                if (NeedBinding)
                    return;

                NeedBinding = true;
                ClusterGeoRefresh?.Invoke(this, this);
            }
        }

        public float AliveCount = 0;

        [XmlIgnore]
        public object Tag = null;

        [XmlIgnore]
        public object RenderTag = null;

        [XmlIgnore]
        public ClusterGeometry Geometry = null;

        [XmlIgnore]
        private object GeoLocker = new object();

        public bool GeoValid()
        {
            lock (GeoLocker)
                return Geometry != null;
        }

        public event EventHandler<Cluster> ClusterDirty = null;
        public event EventHandler<Cluster> ClusterGeoRefresh = null;

        public void DirtyGeo()
        {
            lock (GeoLocker)
                Geometry = null;

            lock (StatusLocker)
                Status = Statuses.Generated;

            ClusterDirty?.Invoke(this, this);
        }

        public void UpdateGeo(ClusterGeometry geo)
        {
            lock (GeoLocker)
                Geometry = geo;

            lock (StatusLocker)
                Status = Statuses.GeometryCreated;

            ClusterGeoRefresh?.Invoke(this,this);
        }
    }
}
