using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld
{
    /// <summary>
    /// Defines the textures and material properties used by all block shapes of this type (dirt, grass, stone, etc...)
    /// </summary>
    public class BlockDef
    {
        public string Name = string.Empty;

        public override string ToString()
        {
            return Name;
        }

        // defines the textures used
        public int Top = -1;
        public int[] Sides = null;
        public int Bottom = -1;

        public bool Transperant = false;

        public BlockDef() { }

        public BlockDef(string name, int top)
        {
            Name = name;
            Top = top;
        }

        public BlockDef(string name, int top, int sides)
        {
            Name = name;
            Top = top;
            Sides = new int[1];
            Sides[0] = sides;
        }

        public BlockDef(string name, int top, int sides, int bottom)
        {
            Name = name;
            Top = top;
            Sides = new int[1];
            Sides[0] = sides;
            Bottom = bottom;
        }

        public BlockDef(string name, int top, int north, int south, int east, int west, int bottom)
        {
            Name = name;
            Top = top;
            Sides = new int[4];
            Sides[0] = north;
            Sides[1] = south;
            Sides[2] = east;
            Sides[3] = west;

            Bottom = bottom;
        }

        public static int EmptyID = -1;
    }

}
