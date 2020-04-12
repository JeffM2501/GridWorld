using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld.Test.Geometry
{
    public static class LoadLimiter
    {
        private static int LoadCount = 0;

        public static int LoadLimit = 2;

        private static List<Cluster.ClusterPos> LoadPriority = new List<Cluster.ClusterPos>();

        public static bool CanLoad(Cluster.ClusterPos pos)
        {
            lock (LoadPriority)
            {
                if (LoadPriority.Count == 0)
                {
                    if (LoadCount >= LoadLimit)
                        return false;
                    LoadCount++;
                    return true;

                }

                int index = LoadPriority.IndexOf(pos);
                if (index < LoadLimit)
                {
                    LoadPriority.RemoveAt(index);
                    LoadCount++;
                    return true;
                }
                return false;
            }
        }

        public static void AddLoadPriority(Cluster.ClusterPos pos)
        {
            lock (LoadPriority)
            {
                if (!LoadPriority.Contains(pos))
                    LoadPriority.Add(pos);
            }
        }

        public static void UpdateFrame()
        {
            LoadCount = 0;
        }
    }
}
