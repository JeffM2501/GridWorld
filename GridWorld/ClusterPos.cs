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

namespace GridWorld
{
    public class ClusterPos
    {
        public static ClusterPos Zero = new ClusterPos(0, 0);

        public Int64 H = 0;
        public Int64 V = 0;

        public ClusterPos() { }
        public ClusterPos(Int64 h, Int64 v) { H = h; V = v; }
        public ClusterPos(ClusterPos pos) { H = pos.H; V = pos.V; }

        public override int GetHashCode()
        {
            return H.GetHashCode() ^ V.GetHashCode();
        }

        public override string ToString()
        {
            return H.ToString() + "," + V.ToString();
        }


        public string ToString(string format)
        {
            return H.ToString(format) + "," + V.ToString(format);
        }

        public override bool Equals(object obj)
        {
            ClusterPos p = obj as ClusterPos;
            if (p == null)
                return false;

            return p.H == H && p.V == V;
        }

        public ClusterPos Offset(Int64 h, Int64 v)
        {
            return new ClusterPos(World.AxisToGrid(H + h), World.AxisToGrid(V + v));
        }

        public ClusterPos OffsetGrid(Int64 h, Int64 v)
        {
            return new ClusterPos(World.AxisToGrid(H + (h * Cluster.HVSize)), World.AxisToGrid(V + (v * Cluster.HVSize)));
        }
    }
}
