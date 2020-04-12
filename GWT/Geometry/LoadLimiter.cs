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

        public static int LoadLimit = 3;

        private static List<ClusterPos> LoadPriority = new List<ClusterPos>();

        static float LastLoad = float.MinValue;

        public static float MinLoadTime = 1.0f / 60.0f;

        public static bool CanLoad(ClusterPos pos)
        {
            float now = Urho.Application.Current.Time.ElapsedTime;
//             if (now - LastLoad < MinLoadTime)
//                 return false;

            lock (LoadPriority)
            {
                if (LoadPriority.Count == 0)
                {
                    if (LoadCount >= LoadLimit)
                        return false;
                    LoadCount++;
                    LastLoad = now;
                    return true;
                }

                int index = LoadPriority.IndexOf(pos);
                if (index >= 0 && index < LoadLimit)
                {
                    LoadPriority.RemoveAt(index);
                    LoadCount++;
                    LastLoad = now;
                    return true;
                }
                return false;
            }
        }

        public static void AddLoadPriority(ClusterPos pos)
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
