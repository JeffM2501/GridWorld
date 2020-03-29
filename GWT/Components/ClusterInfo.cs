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
        public World TheWorld = null;

        public bool Loaded = false;

        public ClusterInfo (Cluster cluster, World world)
        {
            TheCluster = cluster;
            TheWorld = world;
        }

        public override void OnAttachedToNode(Node clusterNode)
        {
            base.OnAttachedToNode(clusterNode);

            if (!Loaded)
            {
                Loaded = true;
                clusterNode.Position = new Vector3(TheCluster.Origin.H, 0, TheCluster.Origin.V);
                clusterNode.SetScale(1);
                ClusterGeometry.BuildGeometry(TheWorld, TheCluster);

                if (TheCluster.Geometry != null)
                {
                    var geos = TheCluster.Geometry.BindToUrhoGeo();

                    var model = clusterNode.CreateComponent<StaticModel>();

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
                    model.Model = meshGroup;

                    index = 0;
                    foreach (var geo in geos)
                    {
                        model.SetMaterial((uint)index, TheWorld.Info.Textures[geo.Item2].RuntimeMat);
                        index++;
                    }
                    model.CastShadows = true;
                }
            }
        }
    }
}
