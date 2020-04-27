using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;
using GridWorld.Test.Geometry;

namespace GridWorld.Test.Components
{
    public class WorldSpaceObject : LogicComponent
    {
        public double WorldSpaceX = 0;
        public double WorldSpaceY = 0;
        public double WorldSpaceZ = 0;

        public WorldSpaceObject()
        {
            GeoLoadManager.OriginChanged += GeoLoadManager_OriginChanged;
        }

        private void GeoLoadManager_OriginChanged(ClusterPos oldOrigin, ClusterPos newOrigin)
        {
            float dh = newOrigin.H - oldOrigin.H;
            float dv = newOrigin.V - oldOrigin.V;

            float newPX = Node.Position.X - dh;
            float newPZ = Node.Position.Z - dv;

            Node.SetWorldPosition(new Vector3(newPX, Node.Position.Y, newPZ));
            UpdateWorldPos();
        }

        protected void UpdateWorldPos()
        {
            if (Node == null)
                return;

            WorldSpaceX = GeoLoadManager.CurrentOrigin.H + Node.Position.X;
            WorldSpaceY = 0 + Node.Position.Y;
            WorldSpaceZ = GeoLoadManager.CurrentOrigin.V + Node.Position.Z;
        }
    }
}
