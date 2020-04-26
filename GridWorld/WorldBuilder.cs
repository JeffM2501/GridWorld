using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using LibNoise;

namespace GridWorld
{
    public class WorldBuilder
    {
        public virtual void Build(string name, string[] paramaters)
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

        public static void NewWorld(string name, string[] paramaters)
        {
            CheckBuilders();

            World.Clear();

            Type btype = typeof(WorldBuilder);
            if (Builders.ContainsKey(name))
                btype = Builders[name];
            else if (Builders.ContainsKey(string.Empty))
                btype = Builders[string.Empty];

            WorldBuilder builder = (WorldBuilder)Activator.CreateInstance(btype);
            builder.Build(builder.Name, paramaters);
        }

        public class FlatBuilder : WorldBuilder
        {
            public FlatBuilder()
            {
                Name = String.Empty;
            }

            public static int Dirt = World.BlockDef.EmptyID;
            public static int Stone = World.BlockDef.EmptyID;
            public static int DeepStone = World.BlockDef.EmptyID;
            public static int Grass = World.BlockDef.EmptyID;
            public static int Water = World.BlockDef.EmptyID;

            public static int Blue = World.BlockDef.EmptyID;
            public static int Red = World.BlockDef.EmptyID;
            public static int Tan = World.BlockDef.EmptyID;

            private static ushort SolidStone = 0;
            private static ushort SolidDirt = 0;
            private static ushort SolidGrass = 0;
            private static ushort FluidWater = 0;
            private static ushort SolidDeepStone = 0;


            private static ushort MakeBlock(int surface, Block.Geometries shape, Block.Directions dir = Block.Directions.None, byte minD = Block.ZeroHeight, byte maxD = Block.FullHeight, bool fluid = false)
            {
                var block = new Block(surface, shape);
                block.Dir = dir;
                block.MinHeight = minD;
                block.MaxHeight = maxD;

                if (fluid)
                {
                    block.Trasperant = true;
                    block.Coolidable = false;
                }

                return World.AddBlock(block);
            }

            public static void InitStandardBlocks()
            {
                if (Dirt != World.BlockDef.EmptyID)
                    return;

                World.Info.Textures.Add(new World.TextureInfo("data/textures/dirt.png"));           //0
                World.Info.Textures.Add(new World.TextureInfo("data/textures/stone.png"));          //1
                World.Info.Textures.Add(new World.TextureInfo("data/textures/grass_top.png"));      //2
                World.Info.Textures.Add(new World.TextureInfo("data/textures/dirt_grass.png"));     //3
                World.Info.Textures.Add(new World.TextureInfo("data/textures/water_trans.xml"));    //4
                World.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_blue.png"));    //5
                World.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_red.png"));     //6
                World.Info.Textures.Add(new World.TextureInfo("data/textures/cotton_tan.png"));     //7
                World.Info.Textures.Add(new World.TextureInfo("data/textures/greystone.png"));      //8

                Dirt = World.AddBlockDef(new World.BlockDef("Dirt", 0));
                Stone = World.AddBlockDef(new World.BlockDef("Stone", 1));
                DeepStone = World.AddBlockDef(new World.BlockDef("DeepStone", 8));
                Grass = World.AddBlockDef(new World.BlockDef("Grass", 2, 3, 0));
                Water = World.AddBlockDef(new World.BlockDef("Water", 4));

                Blue = World.AddBlockDef(new World.BlockDef("Blue", 5));
                Red = World.AddBlockDef(new World.BlockDef("Red", 6));
                Tan = World.AddBlockDef(new World.BlockDef("Tan", 7));

                World.BlockDefs[Water].Transperant = true;

                SolidStone = MakeBlock(Stone, Block.Geometries.Solid);
                SolidDirt = MakeBlock(Dirt, Block.Geometries.Solid);
                SolidGrass = MakeBlock(Grass, Block.Geometries.Solid);
                SolidDeepStone = MakeBlock(DeepStone, Block.Geometries.Solid);
                FluidWater = MakeBlock(Water, Block.Geometries.Solid, Block.Directions.None, Block.ZeroHeight, Block.OpenFluidHeight, true);
            }

            public static void FillClusterDWithBlock(Cluster cluster, int D, ushort index)
            {
                for (Int64 h = 0; h < Cluster.HVSize; h++)
                {
                    for (Int64 v = 0; v < Cluster.HVSize; v++)
                        cluster.SetBlockRelative(h, v, D, index);
                }
            }

            public static void FillClusterDRangeWithBlock(Cluster cluster, int dMin, int dMax, ushort index)
            {
                for (int d = dMin; d < dMax; d++)
                    FillClusterDWithBlock(cluster, d, index);
            }

            public static void FillClusterColumRangeWithBlock(Cluster cluster, Int64 h, Int64 v, int dMin, int dMax, ushort blockIndex)
            {
                for (int d = dMin; d < dMax; d++)
                    cluster.SetBlockRelative(h, v, d, blockIndex);
            }

            public static void FillAreaWithBlock(Cluster cluster, Int64 minH, Int64 minV, Int64 maxH, Int64 maxV, int minD, int maxD, ushort index)
            {
                for (int d = minD; d < maxD; d++)
                {
                    for (Int64 h = minH; h < maxH; h++)
                    {
                        for (Int64 v = minV; v < maxV; v++)
                            cluster.SetBlockRelative(h, v, d, index);
                    }
                }
            }

            protected void AddCrapToCluster(Cluster newCluster)
            {
                int dLevel = 0;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 2, SolidStone);
                dLevel += 2;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 4, SolidDirt);
                dLevel += 4;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 1, SolidGrass);
                dLevel++;


                newCluster.SetBlockRelative(8, 8, dLevel - 1, FluidWater);
                newCluster.SetBlockRelative(9, 8, dLevel - 1, FluidWater);
                newCluster.SetBlockRelative(10, 8, dLevel - 1, FluidWater);
                newCluster.SetBlockRelative(10, 9, dLevel - 1, FluidWater);

                newCluster.SetBlockRelative(2, 10, dLevel, SolidStone);
                newCluster.SetBlockRelative(4, 10, dLevel, MakeBlock(Stone, Block.Geometries.Solid, Block.Directions.None, Block.ZeroHeight, Block.HalfHeight));
                newCluster.SetBlockRelative(6, 10, dLevel, MakeBlock(Stone, Block.Geometries.Solid, Block.Directions.None, Block.HalfHeight, Block.FullHeight)); 
                

                newCluster.SetBlockRelative(2, 18, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North, Block.ZeroHeight, Block.HalfHeight));
                newCluster.SetBlockRelative(4, 18, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South, Block.ZeroHeight, Block.HalfHeight));
                newCluster.SetBlockRelative(6, 18, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East, Block.ZeroHeight, Block.HalfHeight));
                newCluster.SetBlockRelative(8, 18, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West, Block.ZeroHeight, Block.HalfHeight));

                newCluster.SetBlockRelative(2, 20, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North, Block.HalfHeight, Block.FullHeight));
                newCluster.SetBlockRelative(4, 20, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South, Block.HalfHeight, Block.FullHeight));
                newCluster.SetBlockRelative(6, 20, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East, Block.HalfHeight, Block.FullHeight));
                newCluster.SetBlockRelative(8, 20, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West, Block.HalfHeight, Block.FullHeight));

                newCluster.SetBlockRelative(2, 2, dLevel + 2, SolidGrass);


                newCluster.SetBlockRelative(16, 16, dLevel, SolidGrass);
                newCluster.SetBlockRelative(16, 15, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North, Block.ZeroHeight, Block.FullHeight));
                newCluster.SetBlockRelative(15, 15, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.NorthEast, Block.ZeroHeight, Block.FullHeight));
                newCluster.SetBlockRelative(17, 15, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.NorthWest, Block.ZeroHeight, Block.FullHeight));

                newCluster.SetBlockRelative(16, 17, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South, Block.ZeroHeight, Block.FullHeight));
                newCluster.SetBlockRelative(15, 16, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East, Block.ZeroHeight, Block.FullHeight));
                newCluster.SetBlockRelative(17, 16, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West, Block.ZeroHeight, Block.FullHeight));

                newCluster.SetBlockRelative(15, 17, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.SouthEast, Block.ZeroHeight, Block.FullHeight));
                newCluster.SetBlockRelative(17, 17, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.SouthWest, Block.ZeroHeight, Block.FullHeight));

                // make a hole 
                FillAreaWithBlock(newCluster, 20, 16, 22, 25, dLevel - 1, dLevel + 1, World.EmptyBlockIndex);

                FillAreaWithBlock(newCluster, 20, 25, 22, 26, dLevel - 1, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North, Block.ZeroHeight, Block.FullHeight));
                FillAreaWithBlock(newCluster, 20, 15, 22, 16, dLevel - 1, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South, Block.ZeroHeight, Block.FullHeight));

                FillAreaWithBlock(newCluster, 25, 20, 28, 30, dLevel, dLevel + 5, SolidStone);


                FillAreaWithBlock(newCluster, 8, 0, 16, 2, dLevel, dLevel + 5, SolidStone);

                int xCenter = 1;
                int yCetner = 1;

                int dOffset = 4;
                if (newCluster.Origin.H == 0 && newCluster.Origin.V == 0)
                    dOffset = 6;
                if (useOrigin)
                {
                    newCluster.SetBlockRelative(xCenter + 0, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter + 1, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter + 2, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter + 3, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter + 3, yCetner, dLevel + dOffset + 1, MakeBlock(Blue, Block.Geometries.Ramp, Block.Directions.West));

                    newCluster.SetBlockRelative(xCenter, yCetner + 1, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter, yCetner + 2, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                    newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset + 1, MakeBlock(Red, Block.Geometries.Ramp, Block.Directions.South));
                }

//                 if (newCluster.Origin.H < 0)
//                 {
//                     newCluster.SetBlockRelative(16, 16, dLevel + dOffset, new Block(Grass, Block.Geometries.Solid));
//                 }
//                 
//                 
//                 if (newCluster.Origin.V < 0)
//                 {
//                     newCluster.SetBlockRelative(16, 17, dLevel + dOffset, new Block(Stone, Block.Geometry.Solid));
//                 }
            }


            bool useOrigin = false;

            protected void AddCrapToCluster2(Cluster newCluster)
            {
                int dLevel = 0;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 2, SolidStone);
                dLevel += 2;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 4, SolidDirt);
                dLevel += 4;

                FillClusterDRangeWithBlock(newCluster, dLevel, dLevel + 1, SolidGrass);
                dLevel++;

               
                //                 // make a hole 
                //                 FillAreaWithBlock(newCluster, 2, 2, 30, 30, dLevel - 2, dLevel, FluidWater);
                //                 FillAreaWithBlock(newCluster, 14, 14, 18, 18, dLevel - 2, dLevel + 1, SolidStone);

                int xCenter = 16;
                int yCetner = 16;

                //  newCluster.SetBlockRelative(xCenter + 0, yCetner, dLevel + 0, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.SouthWest));

                byte t = Block.HalfHeight;
                byte b = Block.ZeroHeight;

                newCluster.SetBlockRelative(5, 5, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(6, 5, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(7, 5, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(5, 6, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(6, 6, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,Block.FullHeight));
                newCluster.SetBlockRelative(7, 6, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(5, 7, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(6, 7, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));
                newCluster.SetBlockRelative(7, 7, dLevel, MakeBlock(Grass, Block.Geometries.Solid,Block.Directions.None,b,t));


                newCluster.SetBlockRelative(5, 4, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North,b,t));
                newCluster.SetBlockRelative(6, 4, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North,b,t));
                newCluster.SetBlockRelative(7, 4, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.North,b,t));

                newCluster.SetBlockRelative(5, 8, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South,b,t));
                newCluster.SetBlockRelative(6, 8, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South,b,t));
                newCluster.SetBlockRelative(7, 8, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.South,b,t));

                newCluster.SetBlockRelative(4, 5, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East,b,t));
                newCluster.SetBlockRelative(4, 6, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East,b,t));
                newCluster.SetBlockRelative(4, 7, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.East,b,t));

                newCluster.SetBlockRelative(8, 5, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West,b,t));
                newCluster.SetBlockRelative(8, 6, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West,b,t));
                newCluster.SetBlockRelative(8, 7, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.West,b,t));

                newCluster.SetBlockRelative(4, 4, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.NorthEast,b,t));
                newCluster.SetBlockRelative(8, 4, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.NorthWest,b,t));
                newCluster.SetBlockRelative(4, 8, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.SouthEast,b,t));
                newCluster.SetBlockRelative(8, 8, dLevel, MakeBlock(Grass, Block.Geometries.Ramp, Block.Directions.SouthWest,b,t));


                newCluster.SetBlockRelative(xCenter + 2, yCetner+2, dLevel, MakeBlock(Grass, Block.Geometries.Solid));

                int dOffset = 4;
                if (newCluster.Origin.H == 0 && newCluster.Origin.V == 0)
                    dOffset = 6;

                if (useOrigin)
                {
                     newCluster.SetBlockRelative(xCenter + 0, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter + 1, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter + 2, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter + 3, yCetner, dLevel + dOffset, MakeBlock(Blue, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter + 3, yCetner, dLevel + dOffset + 1, MakeBlock(Blue, Block.Geometries.Ramp, Block.Directions.West));
 
                     newCluster.SetBlockRelative(xCenter, yCetner + 1, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter, yCetner + 2, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset, MakeBlock(Red, Block.Geometries.Solid));
                     newCluster.SetBlockRelative(xCenter, yCetner + 3, dLevel + dOffset + 1, MakeBlock(Red, Block.Geometries.Ramp, Block.Directions.South));
                }
            }

            public override void Build(string name, string[] paramaters)
            {
                InitStandardBlocks();

                int HCount = 1;
                int VCount = 1;

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
                        newCluster.Origin = new ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);
                        AddCrapToCluster(newCluster);
                        newCluster.FinalizeGeneration();
                        World.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }

            public void BuildSimple(string name, string[] paramaters)
            {
                InitStandardBlocks();

                int HCount = 1;
                int VCount = 1;

                for (int h = 0; h < HCount; h++)
                {
                    for (int v = 0; v < VCount; v++)
                    {
                        Cluster newCluster = new Cluster();
                        newCluster.Origin = new ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);
                        newCluster.SetBlockRelative(Cluster.HVSize / 2, Cluster.HVSize / 2, Cluster.DSize / 2, SolidStone);
                        newCluster.FinalizeGeneration();
                        World.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }

            protected Perlin DeepPerlin = new Perlin() { Seed = new Random().Next(), Frequency = 0.025, OctaveCount = 1 };
            protected Perlin MidPerlin = new Perlin() { Seed = new Random().Next(), Frequency = 0.025, OctaveCount = 1 };
            protected Perlin HighPerlin = new Perlin() { Seed = new Random().Next(), Frequency = 0.025, OctaveCount = 1 };


            protected void DynamicTerrainCluster(Cluster cluster)
            {
                for (Int64 v = 0; v < Cluster.HVSize; v++)
                {
                    for (Int64 h = 0; h < Cluster.HVSize; h++)
                    {
                        double x = cluster.Origin.H + h;
                        double y = cluster.Origin.V + v;

                        double d = System.Math.Min(System.Math.Abs(DeepPerlin.GetValue(x, 0.5, y)), 1.0);
                        double m = System.Math.Min(System.Math.Abs(MidPerlin.GetValue(x, 0.5, y)), 1.0);
                        double t = System.Math.Min(System.Math.Abs(HighPerlin.GetValue(x, 0.5, y)), 1.0);

                        int deep = (int)(d * 4) + 1;
                        FillClusterColumRangeWithBlock(cluster, h, v, 0, deep, SolidDeepStone);

                        int mid = (int)(m * 6) + 4;
                        if (mid > deep)
                            FillClusterColumRangeWithBlock(cluster, h, v, deep, mid, SolidStone);

                        int high = (int)(t * 13) + 4;
                        if (high > mid)
                            FillClusterColumRangeWithBlock(cluster, h, v, mid, high, SolidDirt);

                        if (cluster.GetBlockRelative(h,v,high-1).DefID == Dirt)
                            cluster.SetBlockRelative(h, v, high, SolidGrass);

//                         double w = System.Math.Min(DeepPerlin.GetValue(x, 10, y), 0);
//                         if (w < 0)
//                         {
//                             int hole = (int)(w * 1);
//                             if (hole < 3)
//                                 hole = 3;
//                             float top = World.DropDepth(h, v);
//                             FillClusterColumRangeWithBlock(cluster, h, v, hole, (int)top+1, 0);
//                         }

                        float realTop = cluster.DropDepth(h, v);
                        if(realTop != float.MinValue && realTop < 6)
                        {
                            FillClusterColumRangeWithBlock(cluster, h, v, (int)realTop, 6, FluidWater);
                        }

                    }
                }
            }

            public void BuildPerlin(string name, string[] paramaters)
            {
                InitStandardBlocks();

                int HCount = 8;
                int VCount = 8;

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
                        newCluster.Origin = new ClusterPos(h * Cluster.HVSize, v * Cluster.HVSize);

                        DynamicTerrainCluster(newCluster);
                        newCluster.FinalizeGeneration();
                        World.Clusters.Add(newCluster.Origin, newCluster);
                    }
                }
            }

            private List<Cluster> NewClusters = new List<Cluster>();

            private void PushNewCluster(Cluster cluster)
            {
                lock (NewClusters)
                    NewClusters.Add(cluster);
            }

            public Cluster[] PopNewClusters()
            {
                Cluster[] clusters = null;
                lock(NewClusters)
                {
                    if (NewClusters.Count > 00)
                    {
                        clusters = NewClusters.ToArray();
                        NewClusters.Clear();
                    }
                }

                return clusters;
            }

            public static bool UseThreads = true;

            public void EnqueCluster(ClusterPos pos)
            {
                try
                {
                    Cluster newCluster = new Cluster();
                    newCluster.Origin = pos;

                    if (!UseThreads)
                    {
                        DynamicTerrainCluster(newCluster);
                        newCluster.FinalizeGeneration();
                    }
                    else
                        ThreadPool.QueueUserWorkItem(x => { DynamicTerrainCluster(newCluster); newCluster.FinalizeGeneration(); });

                    World.Clusters.Add(newCluster.Origin, newCluster);
                    PushNewCluster(newCluster);
                }
                catch (Exception ex)
                {

                    throw;
                }
               
            }
        }
    }
}

