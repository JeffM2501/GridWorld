using GridWorld.Test.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Urho;

namespace GridWorld.Test.Components
{
    public class ClusterInfo : LogicComponent
    {
        public Cluster TheCluster = null;

        private StaticModel GeoModel = null;

        private object Locker = new object();
        private bool TryLoad = false;

        public ClusterInfo (Cluster cluster)
        {
            TheCluster = cluster;
            TheCluster.RenderTag = this;
            ReceiveSceneUpdates = true;

            TheCluster.ClusterGeoRefresh += TheCluster_ClusterGeoRefresh;
        }

        private void TheCluster_ClusterGeoRefresh(object sender, Cluster e)
        {
            lock (Locker)
                TryLoad = true;
        }

        protected override void OnDeleted()
        {
            base.OnDeleted();
            TheCluster.RenderTag = null;
            TheCluster = null;
        }

        private void LoadGeo()
        {
            if (TheCluster.GetStatus() == Cluster.Statuses.GeometryBound)
            {
                lock (Locker)
                    TryLoad = false;
            }

            if (TheCluster.GeoValid() && Geometry.LoadLimiter.CanLoad(TheCluster.Origin))
            {
                lock (Locker)
                    TryLoad = false;

                Node.Position = new Vector3(TheCluster.Origin.H, 0, TheCluster.Origin.V);
                Node.SetScale(1);

                var geos = TheCluster.Geometry.BindToUrhoGeo();

                GeoModel = Node.CreateComponent<StaticModel>();

                var meshGroup = new Model();
                int index = 0;
                meshGroup.NumGeometries = (uint)geos.Count;

                foreach (var geo in geos)
                {
                    meshGroup.SetGeometry((uint)index, 0, geo.Item1);
                    meshGroup.SetGeometryCenter((uint)index, Vector3.Zero);
                    index++;
                }

                meshGroup.BoundingBox = new BoundingBox(new Vector3(0, 0, 0), new Vector3(Cluster.HVSize, Cluster.DSize, Cluster.HVSize));
                GeoModel.Model = meshGroup;

                index = 0;
                foreach (var geo in geos)
                {
                    GeoModel.SetMaterial((uint)index, ClusterGeometry.GeometryBuilder.TheWorld.Info.Textures[geo.Item2].RuntimeMat);
                    index++;
                }
                GeoModel.CastShadows = true;

                TheCluster.FinalizeBind();
            }
        }

        private bool NeedLoad()
        {
            lock (Locker)
                return TryLoad;
        }

        public void CheckLoad()
        {
            if (NeedLoad())
                LoadGeo();
        }

        public override void OnAttachedToNode(Node clusterNode)
        {
            base.OnAttachedToNode(clusterNode);
            CheckLoad();
        }

        protected override void OnUpdate(float timeStep)
        {
            base.OnUpdate(timeStep);
            CheckLoad();

            TheCluster.AliveCount -= timeStep;
            if (TheCluster.AliveCount <= 0)
            {
                if (GeoModel != null)
                {
                    TheCluster.DirtyGeo();
                    Node.RemoveComponent(GeoModel);
                    GeoModel.Dispose();
                    GeoModel = null;
                }
                TheCluster.AliveCount = 0;
            }
        }
    }
}
