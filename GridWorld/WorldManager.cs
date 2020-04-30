using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld
{
    public class WorldManager
    {
        public string CacheFolder = string.Empty;

        protected List<ClusterPos> ClusterIndex = new List<ClusterPos>();
        protected Dictionary<ClusterPos, Cluster> LoadedClusters = new Dictionary<ClusterPos, Cluster>();

        protected List<Cluster> ActiveClusters = new List<Cluster>();
        protected List<Cluster> DirtyClusters = new List<Cluster>();

        protected List<ClusterPos> DesiredClusters = new List<ClusterPos>();

        public virtual void Init(string cacheFolder)
        {

        }

        public virtual void Update()
        {

        }

        public virtual bool PreloadCluster(ClusterPos pos)
        {
            return false;
        }
    }
}
