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

namespace GridWorld.Test.Geometry
{
    public static class LoadLimiter
    {
        private static int LoadCount = 0;

        public static int LoadLimit = 16;

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
