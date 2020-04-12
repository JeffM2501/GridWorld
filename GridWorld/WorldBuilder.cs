using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GridWorld
{
    public class WorldBuilder
    {
        public virtual void Build(string name, string[] paramaters, World world)
        {

        }

        public string Name = string.Empty;

        protected static Dictionary<string, Type> Builders = new Dictionary<string, Type>();

        public void Register(Type builder)
        {
            if (builder.IsSubclassOf(typeof(WorldBuilder)))
            {
                WorldBuilder b = (WorldBuilder)Activator.CreateInstance(builder);
                Builders.Add(b.Name, builder);
            }
        }

        protected static void CheckBuilders()
        {
            if (Builders.ContainsKey(string.Empty))
                return;

            Builders.Add(string.Empty, typeof(FlatBuilder));
        }

        public static World NewWorld(string name, string[] paramaters)
        {
            CheckBuilders();

            World world = new World();
            Type btype = typeof(WorldBuilder);
            if (Builders.ContainsKey(name))
                btype = Builders[name];
            else if (Builders.ContainsKey(string.Empty))
                btype = Builders[string.Empty];

            WorldBuilder builder = (WorldBuilder)Activator.CreateInstance(btype);
            builder.Build(builder.Name, paramaters, world);

            ClusterGeometry.BuildGeometry(world);
            return world;
        }

        public class FlatBuilder : WorldBuilder
        {
            public FlatBuilder()
            {
                Name = String.Empty;
            }

            public static int Dirt = World.BlockDef.EmptyID;
            public static int Stone = World.BlockDef.EmptyID;
            public static int Grass = World.BlockDef.EmptyID;
            public static int Water = World.BlockDef.EmptyID;

            public static int Blue = World.BlockDef.EmptyID;
            public static int Red = World.BlockDef.EmptyID;
            public static int Tan = World.BlockDef.EmptyID;

            public static void InitStandardBlocks(World world)
            {
                if (Dirt != World.BlockDef.EmptyID)
                    return;

                world.Info.Textures.Add(new World.TextureInfo("data/textures/dirt.png"));           //0
                world.Info.Textures.Add(new World.TextureInfo("data/textures/stone.png"));          //1
                world.Info.Textures.Add(new World.TextureInfo("data/textures/grass_top.png"));      //2
                world.Info.Textures.Add(new World.TextureInfo("data/textures/dirt_grass.png"));     //3
                world.Info.Textures.Add(new World.TextureInfo("data/textures/water_trans.xml"));    //4
                world.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_blue.png"));    //5
                world.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_red.png"));     //6
                world.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_tan.png"));     //7

                Dirt = world.AddBlockDef(new World.BlockDef("Dirt", 0));
                Stone = world.AddBlockDef(new World.BlockDef("Stone", 1));
                Grass = world.AddBlockDef(new World.BlockDef("Grass", 2, 3, 0));
                Water = world.AddBlockDef(new World.BlockDef("Water", 4));

                Blue = world.AddBlockDef(new World.BlockDef("Blue", 5));
                Red = world.AddBlockDef(new World.BlockDef("Red", 6));
                Tan = world.AddBlockDef(new World.BlockDef("Tan", 7));

                world.BlockDefs[Water].Transperant = true;
            }

            public static void FillClusterDWithBlock(Cluster cluster, int D, int blockID, Cluster.Block.Geometry geo)
            {
                for (int h = 0; h < Cluster.HVSize; h++)
                {
                    for (int v = 0; v < Cluster.HVSize; v++)
                        cluster.SetBlockRelative(h, v, D, new Cluster.Block(blockID, geo));
                }
            }

            public static void FillClusterDRangeWithBlock(Cluster cluster, int dMin, int dMax, int blockID, Cluster.Block.Geometry geo)
            {
                for (int d = dMin; d < dMax; d++)
                    FillClusterDWithBlock(cluster, d, blockID, geo);
            }

            public static void FillAreaWithBlock(Cluster cluster, int minH, int minV, int maxH, int maxV, int minD, int maxD, int blockID, Cluster.Block.Geometry geo)
            {
                for (int d = minD; d < maxD; d++)
                {
                    for (int h = minH; h < maxH; h++)
                    {
                        for (int v = minV; v < maxV; v++)
                            cluster.SetBlockRelative(h, v, d, new Cluster.Block(blockID, geo));
                    }
                }
            }

            protected void AddCrapToCluster(Cluster newCluster)
            {
                int dLevel = 0;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 2, Stone, Cluster.Block.Geometry.Solid);
                dLevel += 2;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 4, Dirt, Cluster.Block.Geometry.Solid);
                dLevel += 4;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 1, Grass, Cluster.Block.Geometry.Solid);
                dLevel++;


                newCluster.SetBlockRelative(8, 8, dLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(9, 8, dLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(10, 8, dLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(10, 9, dLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));

                newCluster.SetBlockRelative(2, 10, dLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(4, 10, dLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.HalfLower));
                newCluster.SetBlockRelative(6, 10, dLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.HalfUpper));

                newCluster.SetBlockRelative(2, 18, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthHalfLowerRamp));
                newCluster.SetBlockRelative(4, 18, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthHalfLowerRamp));
                newCluster.SetBlockRelative(6, 18, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastHalfLowerRamp));
                newCluster.SetBlockRelative(8, 18, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestHalfLowerRamp));

                newCluster.SetBlockRelative(2, 20, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthHalfUpperRamp));
                newCluster.SetBlockRelative(4, 20, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthHalfUpperRamp));
                newCluster.SetBlockRelative(6, 20, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastHalfUpperRamp));
                newCluster.SetBlockRelative(8, 20, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestHalfUpperRamp));

                newCluster.SetBlockRelative(2, 2, dLevel + 2, new Cluster.Block(Grass, Cluster.Block.Geometry.Solid));


                newCluster.SetBlockRelative(16, 16, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(16, 15, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthFullRamp));
                newCluster.SetBlockRelative(16, 17, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthFullRamp));
                newCluster.SetBlockRelative(15, 16, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastFullRamp));
                newCluster.SetBlockRelative(17, 16, dLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestFullRamp));

                // make a hole 
                FillAreaWithBlock(newCluster, 20, 16, 22, 25, dLevel - 1, dLevel + 1, World.BlockDef.EmptyID, Cluster.Block.Geometry.Empty);

                FillAreaWithBlock(newCluster, 20, 25, 22, 26, dLevel - 1, dLevel, Grass, Cluster.Block.Geometry.NorthFullRamp);
                FillAreaWithBlock(newCluster, 20, 15, 22, 16, dLevel - 1, dLevel, Grass, Cluster.Block.Geometry.SouthFullRamp);

                FillAreaWithBlock(newCluster, 25, 20, 28, 30, dLevel, dLevel + 5, Stone, Cluster.Block.Geometry.Solid);


                FillAreaWithBlock(newCluster, 8, 0, 16, 2, dLevel, dLevel + 5, Stone, Cluster.Block.Geometry.Solid);

                int dOffset = 4;
                if (newCluster.Origin.H == 0 && newCluster.Origin.V == 0)
                    dOffset = 6;

                newCluster.SetBlockRelative(0, 0, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(1, 0, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(2, 0, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(3, 0, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(3, 0, dLevel + dOffset+1, new Cluster.Block(Blue, Cluster.Block.Geometry.WestFullRamp));

                newCluster.SetBlockRelative(0, 1, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(0, 2, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(0, 3, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(0, 3, dLevel + dOffset + 1, new Cluster.Block(Red, Cluster.Block.Geometry.SouthFullRamp));

                if (newCluster.Origin.H < 0)
                {
                    newCluster.SetBlockRelative(16, 16, dLevel + dOffset, new Cluster.Block(Grass, Cluster.Block.Geometry.Solid));
                }


                if (newCluster.Origin.V < 0)
                {
                    newCluster.SetBlockRelative(16, 17, dLevel + dOffset, new Cluster.Block(Stone, Cluster.Block.Geometry.Solid));
                }
            }

            protected void AddCrapToCluster2(Cluster newCluster)
            {
                int dLevel = 0;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 2, Stone, Cluster.Block.Geometry.Solid);
                dLevel += 2;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 4, Dirt, Cluster.Block.Geometry.Solid);
                dLevel += 4;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 1, Grass, Cluster.Block.Geometry.Solid);
                dLevel++;

                // make a hole 
                FillAreaWithBlock(newCluster, 1, 1, 31, 31, dLevel - 2, dLevel, Water, Cluster.Block.Geometry.Fluid);

                int xCenter = 16;
                int yCetner = 16;

                int dOffset = 4;
                if (newCluster.Origin.H == 0 && newCluster.Origin.V == 0)
                    dOffset = 6;

                newCluster.SetBlockRelative(xCenter + 0, yCetner, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter + 1,yCetner, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter + 2,yCetner, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter + 3,yCetner, dLevel + dOffset, new Cluster.Block(Blue, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter + 3,yCetner, dLevel + dOffset + 1, new Cluster.Block(Blue, Cluster.Block.Geometry.WestFullRamp));

                newCluster.SetBlockRelative(xCenter, yCetner + 1, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter, yCetner + 2, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset, new Cluster.Block(Red, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset + 1, new Cluster.Block(Red, Cluster.Block.Geometry.SouthFullRamp));
            }

            public override void Build(string name, string[] paramaters, World world)
            {
                InitStandardBlocks(world);

                int HCount = 32;
                int VCount = 32;

                int hMin = 0;
                if (HCount > 1)
                    hMin = HCount / -2;

                int hMax = HCount + hMin;

                int vMin = 0;
                if (VCount > 1)
                    vMin = VCount / -2;
                int vMax = VCount + vMin;

                for (int h = hMin; h < hMax; h++)
                {
                    for (int v = vMin; v < vMax; v++)
                    {
                        Cluster newCluster = new Cluster();
                        newCluster.Origin = new Cluster.ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);
                        AddCrapToCluster2(newCluster);
                        newCluster.FinalizeGeneration();
                        world.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }

            public void BuildSimple(string name, string[] paramaters, World world)
            {
                InitStandardBlocks(world);

                int HCount = 1;
                int VCount = 1;

                for (int h = 0; h < HCount; h++)
                {
                    for (int v = 0; v < VCount; v++)
                    {
                        Cluster newCluster = new Cluster();
                        newCluster.Origin = new Cluster.ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);
                        newCluster.SetBlockRelative(Cluster.HVSize/2, Cluster.HVSize/2, Cluster.DSize/2, new Cluster.Block(Water, Cluster.Block.Geometry.Solid));
                        world.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }
        }
    }
}

