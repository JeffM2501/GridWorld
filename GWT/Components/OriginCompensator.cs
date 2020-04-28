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
