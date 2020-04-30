using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld.WorldManagers
{
    public class FixedSizeMap : WorldManager
    {
        public event EventHandler SeedMap = null;

        public override void Init(string cacheFolder)
        {
            base.Init(cacheFolder);
            DirectoryInfo dir = new DirectoryInfo(cacheFolder);

            if (dir.Exists)
            {
                foreach (var file in dir.GetFiles("*.cluster"))
                { 

                }
            }

            if (ClusterIndex.Count == 0)
                SeedMap?.Invoke(this, EventArgs.Empty);
                
        }
    }
}
