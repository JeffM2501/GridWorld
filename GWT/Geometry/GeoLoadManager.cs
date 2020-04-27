using GridWorld.Test.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Urho;

namespace GridWorld.Test.Geometry
{
    public static class GeoLoadManager
    {
        public delegate void GeoLoadCallback(Cluster theCluster, object tag);

        public static ClusterPos CurrentOrigin = new ClusterPos(0, 0);

        public delegate void OriginChangeCallback(ClusterPos oldOrigin, ClusterPos newOrigin);

        public static event OriginChangeCallback OriginChanged = null;

        public static void SetOrigin(Int64 h, Int64 v)
        {
            ClusterPos oldOriign = CurrentOrigin;
            CurrentOrigin = new ClusterPos(h,v);
            OriginChanged?.Invoke(oldOriign, CurrentOrigin);
        }

        public static Int64 OriginLimit = 1024;
        public static Int64 OriginSnap = 512;

        public static void CheckOrigin(Vector3 cameraPos)
        {
            if (cameraPos.X > OriginLimit || cameraPos.X < -OriginLimit || cameraPos.Z > OriginLimit || cameraPos.Z < -OriginLimit)
            {
                Int64 h = CurrentOrigin.H + (Int64)cameraPos.X;
                Int64 v = CurrentOrigin.V + (Int64)cameraPos.Z;

                h = h / OriginSnap;
                v = v / OriginSnap;

                h = h * OriginSnap;
                v = v * OriginSnap;

                SetOrigin(h, v);
            }
        }

        public static int ForceLoadRadius = 4;

        private static bool UseThreads = false;

        public static void GenerateGeometry(Cluster theCluster, object tag, GeoLoadCallback callback)
        {
            if (UseThreads)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(o =>
                {
                    ClusterGeometry.BuildGeometry(theCluster);
                    callback?.Invoke(theCluster, tag);
                }));
            }
            else
            {
                ClusterGeometry.BuildGeometry(theCluster);
                callback?.Invoke(theCluster, tag);
            }
        }

        public delegate void CuslterPosCallback(ClusterPos pos);
        public static event CuslterPosCallback NeedCluster = null;

        public static float ClusterZombieTime = 10;

        private static void ForceClusterLoad(ClusterPos origin, int h, int v, bool display)
        {
            var newPos = origin.Offset(h, v);
            var cluster = World.ClusterFromPosition(newPos);
            if (cluster == null)
            {
                // it's off the current map, flag it for generation, another cycle will pick it up
                NeedCluster?.Invoke(newPos);
                return;
            }

            if (!display)
                return;

            cluster.AliveCount = ClusterZombieTime;

            var status = cluster.GetStatus();
            if (status == Cluster.Statuses.Raw || status == Cluster.Statuses.GeometryBound || status == Cluster.Statuses.GeometryPending)
                return; // it's good to go, or is waiting on generation

            if (status == Cluster.Statuses.GeometryCreated)
            {
                // the geo was created, it just isn't bound, tell the Urho component to put a load in the pipe
                cluster.RequestBinding();
                LoadLimiter.AddLoadPriority(cluster.Origin);
                return;
            }

            GeoLoadManager.GenerateGeometry(cluster, null, (c, t) => { LoadLimiter.AddLoadPriority(cluster.Origin); });
        }

        private static void ForceRingLoad(int radius, ClusterPos origin, bool display)
        {
            int radUnits = radius * Cluster.HVSize;
            for (int i = 0; i <= radUnits; i+= Cluster.HVSize)
            {
                ForceClusterLoad(origin, radUnits, i, display);
                ForceClusterLoad(origin, -radUnits, i, display);

                ForceClusterLoad(origin, i, radUnits, display);
                ForceClusterLoad(origin, i,-radUnits, display);

               // if (i != 0)
                {
                    ForceClusterLoad(origin, radUnits, -i, display);
                    ForceClusterLoad(origin, -radUnits, -i, display);

                    ForceClusterLoad(origin, -i, radUnits, display);
                    ForceClusterLoad(origin, -i, -radUnits, display);
                }
            }
        }

        public static void UpdateGeoForPosition(Vector3 cameraPos)
        {
            Int64 h = (Int64)cameraPos.X + CurrentOrigin.H;
            Int64 v = (Int64)cameraPos.Z + CurrentOrigin.V;
            Int64 d = (Int64)cameraPos.Y + 0;

            var rootCluster = World.ClusterFromPosition(h,v,d);
            if (rootCluster == null)
                return;

            ForceClusterLoad(rootCluster.Origin,0,0, true);

            for (int i = 1; i <= ForceLoadRadius; i++)
            {
                ForceRingLoad(i, rootCluster.Origin,true);
            }

            for (int i = ForceLoadRadius; i <= ForceLoadRadius*2; i++)
            {
                ForceRingLoad(i, rootCluster.Origin, false);
            }
        }
    }
}
