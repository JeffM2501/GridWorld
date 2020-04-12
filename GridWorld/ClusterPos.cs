﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld
{
    public class ClusterPos
    {
        public static ClusterPos Zero = new ClusterPos(0, 0);

        public int H = 0;
        public int V = 0;

        public ClusterPos() { }
        public ClusterPos(int h, int v) { H = h; V = v; }
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

        public ClusterPos Offset(int h, int v)
        {
            return new ClusterPos(World.AxisToGrid(H + h), World.AxisToGrid(V + v));
        }

        public ClusterPos OffsetGrid(int h, int v)
        {
            return new ClusterPos(World.AxisToGrid(H + (h * Cluster.HVSize)), World.AxisToGrid(V + (v * Cluster.HVSize)));
        }
    }
}