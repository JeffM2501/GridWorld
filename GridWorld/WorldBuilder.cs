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

            public static void InitMCBlocks(World world)
            {
                world.Info.Textures.Add(new World.TextureInfo("textures/spritesheet_tiles.png", 8, 16));
                //world.Info.Textures.Add(new World.TextureInfo("world/grid.png",1,1));
                if (Dirt != World.BlockDef.EmptyID)
                    return;

                Dirt = world.AddBlockDef(new World.BlockDef("Dirt", 13));
                Stone = world.AddBlockDef(new World.BlockDef("Stone", 4));
                Grass = world.AddBlockDef(new World.BlockDef("Grass", 67, 6, 14));
                Water = world.AddBlockDef(new World.BlockDef("Water", 64));

                world.BlockDefs[Water].Transperant = true;
            }

            public static void FillClusterZWithBlock(Cluster cluster, int Z, int blockID, Cluster.Block.Geometry geo)
            {
                for (int x = 0; x < Cluster.XYSize; x++)
                {
                    for (int y = 0; y < Cluster.XYSize; y++)
                        cluster.SetBlockRelative(x, y, Z, new Cluster.Block(blockID, geo));
                }
            }

            public static void FillClusterZRangeWithBlock(Cluster cluster, int zMin, int zMax, int blockID, Cluster.Block.Geometry geo)
            {
                for (int z = zMin; z < zMax; z++)
                    FillClusterZWithBlock(cluster, z, blockID, geo);
            }

            public static void FillAreaWithBlock(Cluster cluster, int minX, int minY, int maxX, int maxY, int minZ, int maxZ, int blockID, Cluster.Block.Geometry geo)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    for (int x = minX; x < maxX; x++)
                    {
                        for (int y = minY; y < maxY; y++)
                            cluster.SetBlockRelative(x, y, z, new Cluster.Block(blockID, geo));
                    }
                }
            }

            protected void AddCrapToCluster(Cluster newCluster)
            {
                int zLevel = 0;

                FillClusterZRangeWithBlock(newCluster, zLevel, zLevel + 2, Stone, Cluster.Block.Geometry.Solid);
                zLevel += 2;

                FillClusterZRangeWithBlock(newCluster, zLevel, zLevel + 4, Dirt, Cluster.Block.Geometry.Solid);
                zLevel += 4;

                FillClusterZRangeWithBlock(newCluster, zLevel, zLevel + 1, Grass, Cluster.Block.Geometry.Solid);
                zLevel++;


                newCluster.SetBlockRelative(8, 8, zLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(9, 8, zLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(10, 8, zLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));
                newCluster.SetBlockRelative(10, 9, zLevel - 1, new Cluster.Block(Water, Cluster.Block.Geometry.Fluid));

                newCluster.SetBlockRelative(2, 10, zLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(4, 10, zLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.HalfLower));
                newCluster.SetBlockRelative(6, 10, zLevel, new Cluster.Block(Stone, Cluster.Block.Geometry.HalfUpper));

                newCluster.SetBlockRelative(2, 18, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthHalfLowerRamp));
                newCluster.SetBlockRelative(4, 18, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthHalfLowerRamp));
                newCluster.SetBlockRelative(6, 18, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastHalfLowerRamp));
                newCluster.SetBlockRelative(8, 18, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestHalfLowerRamp));

                newCluster.SetBlockRelative(2, 20, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthHalfUpperRamp));
                newCluster.SetBlockRelative(4, 20, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthHalfUpperRamp));
                newCluster.SetBlockRelative(6, 20, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastHalfUpperRamp));
                newCluster.SetBlockRelative(8, 20, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestHalfUpperRamp));

                newCluster.SetBlockRelative(2, 2, zLevel + 2, new Cluster.Block(Grass, Cluster.Block.Geometry.Solid));


                newCluster.SetBlockRelative(16, 16, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.Solid));
                newCluster.SetBlockRelative(16, 15, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.NorthFullRamp));
                newCluster.SetBlockRelative(16, 17, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.SouthFullRamp));
                newCluster.SetBlockRelative(15, 16, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.EastFullRamp));
                newCluster.SetBlockRelative(17, 16, zLevel, new Cluster.Block(Grass, Cluster.Block.Geometry.WestFullRamp));

                // make a hole 
                FillAreaWithBlock(newCluster, 20, 16, 22, 25, zLevel - 1, zLevel + 1, World.BlockDef.EmptyID, Cluster.Block.Geometry.Empty);

                FillAreaWithBlock(newCluster, 20, 25, 22, 26, zLevel - 1, zLevel, Grass, Cluster.Block.Geometry.NorthFullRamp);
                FillAreaWithBlock(newCluster, 20, 15, 22, 16, zLevel - 1, zLevel, Grass, Cluster.Block.Geometry.SouthFullRamp);

                FillAreaWithBlock(newCluster, 25, 20, 28, 30, zLevel, zLevel + 5, Stone, Cluster.Block.Geometry.Solid);
            }

            public override void Build(string name, string[] paramaters, World world)
            {
                InitMCBlocks(world);

                int XCount = 5;
                int YCount = 5;

                for (int x = 0; x < XCount; x++)
                {
                    for (int y = 0; y < YCount; y++)
                    {
                        Cluster newCluster = new Cluster();
                        newCluster.Origin = new Cluster.ClusterPos(x * Cluster.XYSize, y * Cluster.XYSize);
                        AddCrapToCluster(newCluster);
                        world.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }
        }
    }
}

