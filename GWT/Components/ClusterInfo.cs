#region copyright
/*
GridWorld a learning experiement in voxel technology
Copyright (c) 2020 Jeffery Myersn

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

using GridWorld.Test.Geometry;

namespace GridWorld.Test.Components
{
    public class ClusterInfo : OriginCompensator
    {
        public Cluster TheCluster = null;

        private StaticModel GeoModel = null;

        private object Locker = new object();
        private bool TryLoad = false;

        private Node TimmerMarker = null;
        private Node TimerOrigin = null;

        private bool ShowTimers = false;

        private bool UpdateOrigin = false;
        private Vector3 NewOrigin = Vector3.Zero;

        public ClusterInfo (Cluster cluster) : base()
        {
            TheCluster = cluster;
            TheCluster.RenderTag = this;
            ReceiveSceneUpdates = true;
            WorldPos = TheCluster.Origin;
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

            if (TheCluster.GeoValid() && LoadLimiter.CanLoad(TheCluster.Origin))
            {
                lock (Locker)
                    TryLoad = false;

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
                    GeoModel.SetMaterial((uint)index, World.Info.Textures[geo.Item2].RuntimeMat);
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

            Node.SetWorldPosition(new Vector3((float)(TheCluster.Origin.H - GeoLoadManager.CurrentOrigin.H), 0, (float)(TheCluster.Origin.V - GeoLoadManager.CurrentOrigin.V)));
            Node.SetScale(1);

            CheckLoad();

            var NormalMaterial = Material.FromColor(Color.Blue);
            var ErrorMaterial = Material.FromColor(Color.Red);

            if (ShowTimers)
            {
                Node TimerOrigin = clusterNode.CreateChild();
                var orginMesh = TimerOrigin.CreateComponent<StaticModel>();
                orginMesh.Model = Urho.CoreAssets.Models.Box;
                orginMesh.Material = ErrorMaterial;
                TimerOrigin.Position = new Vector3(0, 24, 0); ;

                TimmerMarker = TimerOrigin.CreateChild();
                var tipMesh = TimmerMarker.CreateComponent<StaticModel>();
                tipMesh.Model = Urho.CoreAssets.Models.Cone;
                tipMesh.Material = NormalMaterial;
                TimmerMarker.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, 180);
                TimmerMarker.Position = new Vector3(0, 10, 0);
            }
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
                    Node.RemoveComponent(GeoModel);
                    GeoModel.Dispose();
                    GeoModel = null;

                    TheCluster.DirtyGeo();
                }
                TheCluster.AliveCount = 0;
            }

            if (ShowTimers)
            {
                if (GeoModel == null && TheCluster.AliveCount > 0)
                    TimmerMarker.Position = new Vector3(0, -1, 0);
                else
                    TimmerMarker.Position = new Vector3(0, TheCluster.AliveCount, 0);
            }
        }
    }
}
