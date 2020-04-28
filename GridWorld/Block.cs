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
    public enum Directions
    {
        None = 0,
        North,
        South,
        East,
        West,
        NorthWest,
        NorthEast,
        SouthWest,
        SouthEast,
    }

    public class Block : IEquatable<Block>
    {
        public enum Geometries
        {
            Empty,
            Invisible,
            Solid,
            FullRamp,
            LowerRamp,
        }

        public Geometries Geom = Geometries.Empty;
        public Directions Dir = Directions.None;

        public const byte FullHeight = 8;
        public const byte OpenFluidHeight = 7;
        public const byte ThreeQuarterHeight = 6;
        public const byte HalfHeight = 4;
        public const byte QuarterHeight = 2;
        public const byte ZeroHeight = 0;
        public const byte BlockHeightIncrement = 1;

        public byte MinHeight = ZeroHeight;
        public byte MaxHeight = FullHeight;

        public float GetMaxD() { return MaxHeight / 8.0f; }
        public float GetMinD() { return MinHeight / 8.0f; }

        public bool Trasperant = false;
        public bool Coolidable = true;

        public int DefID;

        public object RenderTag = null;

        public static Block Empty = new Block(World.BlockDef.EmptyID, Geometries.Empty);
        public static Block Invalid = new Block(World.BlockDef.EmptyID, Geometries.Empty);

        public Block() { }

        public Block(int id, Geometries geo)
        {
            DefID = id;
            Geom = geo;
        }

        public override int GetHashCode()
        {
            return DefID.GetHashCode() ^ Geom.GetHashCode() ^ Dir.GetHashCode() ^ MaxHeight.GetHashCode() ^ MinHeight.GetHashCode();
        }

        public float GetDForLocalPosition(float h, float v)
        {
            if (Geom == Geometries.Empty || !Coolidable)
                return float.MinValue;

            float invH = 1 - h;
            float invV = 1 - v;

            float max = MaxHeight / 8.0f;
            float min = MinHeight / 8.0f;
            float delta = max - min;

            switch (Geom)
            {
                case Block.Geometries.Solid:
                case Block.Geometries.Invisible:
                    return max;

                case Block.Geometries.FullRamp:
                case Block.Geometries.LowerRamp:
                    switch (Dir)
                    {
                        case Directions.North:
                            return (v * delta) + min;

                        case Directions.South:
                            return (invV * delta) + min;

                        case Directions.East:
                            return (h * delta) + min;

                        case Directions.West:
                            return (invH * delta) + min;

                        case Directions.NorthWest:
                            if (v <= invH)
                                return (v * delta) + min;
                            else
                                return (invH * delta) + min;

                        case Directions.NorthEast:
                            if (v <= invH)
                                return (h * delta) + min;
                            else
                                return (v * delta) + min;

                        case Directions.SouthEast:
                            if (v <= invH)
                                return (invV * delta) + min;
                            else
                                return (h * delta) + min;

                        case Directions.SouthWest:
                            if (v <= invH)
                                return (invH * delta) + min;
                            else
                                return (invV * delta) + min;
                    }

                    break;
            }

            return float.MinValue;
        }

        public bool Equals(Block other)
        {
            return other != null && other.DefID == DefID && other.Geom == Geom && other.Dir == Dir && other.MinHeight == MinHeight && other.MaxHeight == MaxHeight;
        }
    }
}
