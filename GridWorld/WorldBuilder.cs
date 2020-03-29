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

            public static void InitStandardBlocks(World world)
            {
                world.Info.Textures.Add(new World.TextureInfo("data/textures/dirt.png"));
                world.Info.Textures.Add(new World.TextureInfo("data/textures/stone.png"));
                world.Info.Textures.Add(new World.TextureInfo("data/textures/grass_top.png"));
                world.Info.Textures.Add(new World.TextureInfo("data/textures/dirt_grass.png"));
                world.Info.Textures.Add(new World.TextureInfo("data/textures/water_trans.xml"));
                //    world.Info.Textures.Add(new World.TextureInfo("data/textures/spritesheet_tiles.png", 8, 16));
                //world.Info.Textures.Add(new World.TextureInfo("world/grid.png",1,1));
                if (Dirt != World.BlockDef.EmptyID)
                    return;

                Dirt = world.AddBlockDef(new World.BlockDef("Dirt", 0));
                Stone = world.AddBlockDef(new World.BlockDef("Stone", 1));
                Grass = world.AddBlockDef(new World.BlockDef("Grass", 2, 3, 0));
                Water = world.AddBlockDef(new World.BlockDef("Water", 4));

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
            }

            public override void Build(string name, string[] paramaters, World world)
            {
                InitStandardBlocks(world);

                int HCount = 5;
                int VCount = 5;

                int hMin = HCount / -2;
                int hMax = HCount + hMin;

                int vMin = VCount / -2;
                int vMax = VCount + vMin;

                for (int h = hMin; h < hMax; h++)
                {
                    for (int v = vMin; v < vMax; v++)
                    {
                        Cluster newCluster = new Cluster();
                        newCluster.Origin = new Cluster.ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);
                        AddCrapToCluster(newCluster);
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

