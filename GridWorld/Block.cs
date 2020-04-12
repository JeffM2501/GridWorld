using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld
{
    public class Block : IEquatable<Block>
    {
        public enum Geometry
        {
            Empty,
            Solid,
            Fluid,
            NorthFullRamp,
            SouthFullRamp,
            EastFullRamp,
            WestFullRamp,
            HalfUpper,
            HalfLower,
            NorthHalfLowerRamp,
            SouthHalfLowerRamp,
            EastHalfLowerRamp,
            WestHalfLowerRamp,
            NorthHalfUpperRamp,
            SouthHalfUpperRamp,
            EastHalfUpperRamp,
            WestHalfUpperRamp,
        }

        public int DefID;

        public Geometry Geom;

        public object RenderTag = null;

        public static Block Empty = new Block(World.BlockDef.EmptyID, Geometry.Empty);
        public static Block Invalid = new Block(World.BlockDef.EmptyID, Geometry.Empty);

        public Block() { }

        public Block(int id, Geometry geo)
        {
            DefID = id;
            Geom = geo;
        }

        public override int GetHashCode()
        {
            return DefID.GetHashCode() ^ Geom.GetHashCode();
        }

        public float GetDForLocalPosition(float h, float v)
        {
            if (Geom == Geometry.Empty)
                return float.MinValue;

            float invH = 1 - h;
            float invV = 1 - v;

            switch (Geom)
            {
                case Block.Geometry.Solid:
                case Block.Geometry.HalfUpper:
                    return 1;

                case Block.Geometry.HalfLower:
                    return 0.5f;

                case Block.Geometry.NorthFullRamp:
                    return v;

                case Block.Geometry.SouthFullRamp:
                    return 1.0f - v;

                case Block.Geometry.EastFullRamp:
                    return h;

                case Block.Geometry.WestFullRamp:
                    return 1.0f - h;

                case Block.Geometry.NorthHalfLowerRamp:
                    return v * 0.5f;

                case Block.Geometry.SouthHalfLowerRamp:
                    return (1.0f - v) * 0.5f;

                case Block.Geometry.EastHalfLowerRamp:
                    return h * 0.5f;

                case Block.Geometry.WestHalfLowerRamp:
                    return (1.0f - h) * 0.5f;

                case Block.Geometry.NorthHalfUpperRamp:
                    return (v * 0.5f) + 0.5f;

                case Block.Geometry.SouthHalfUpperRamp:
                    return ((1.0f - v) * 0.5f) + 0.5f;

                case Block.Geometry.EastHalfUpperRamp:
                    return (h * 0.5f) + 0.5f;

                case Block.Geometry.WestHalfUpperRamp:
                    return ((1.0f - h) * 0.5f) + 0.5f;
            }

            return float.MinValue;
        }

        public bool Equals(Block other)
        {
            return other != null && other.DefID == DefID && other.Geom == Geom;
        }
    }
}
