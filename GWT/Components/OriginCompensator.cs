
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GridWorld.Test.Geometry;

using Urho;

namespace GridWorld.Test.Components
{
    public class OriginCompensator : LogicComponent
    {
        public ClusterPos WorldPos = null;
        public Vector3 Offset = Vector3.Zero;

        public OriginCompensator()
        {
            GeoLoadManager.OriginChanged += GeoLoadManager_OriginChanged;
        }

        private void GeoLoadManager_OriginChanged(ClusterPos oldOrigin, ClusterPos newOrigin)
        {
            if (WorldPos == null)
                return;

            Node.SetWorldPosition(new Vector3((float)(WorldPos.H - newOrigin.H) + Offset.X, Offset.Y, (float)(WorldPos.V - newOrigin.V) + Offset.Z));
        }
    }
}
