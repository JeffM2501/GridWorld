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
using System.IO;
using System.Text;
using System.Threading.Tasks;

using MsgPack.Serialization;

namespace GridWorld
{
    public class WorldManager
    {
        public string CacheFolder = string.Empty;

        protected List<ClusterPos> ClusterIndex = new List<ClusterPos>();
        protected Dictionary<ClusterPos, Cluster> LoadedClusters = new Dictionary<ClusterPos, Cluster>();

        protected List<Cluster> ActiveClusters = new List<Cluster>();
        protected List<Cluster> DirtyClusters = new List<Cluster>();

        protected List<ClusterPos> DesiredClusters = new List<ClusterPos>();

        public int ClusterSize = Cluster.HVSize * Cluster.HVSize * Cluster.DSize * sizeof(UInt16);

        protected FileInfo HeaderFile = null;

        public delegate void ClusterEventHandler(Cluster cluster);
        public event ClusterEventHandler ClusterLoaded = null;

        public event EventHandler WorldInited = null;

        public virtual void Init(string cacheFolder)
        {

        }

        public virtual void Update()
        {

        }

        public virtual bool PreloadCluster(ClusterPos pos)
        {
            return false;
        }

        public virtual void LoadCluster(ClusterPos pos)
        {

        }

        public virtual void DirtyCluster(Cluster cluster)
        {

        }

        public class WorldHeader
        {
            public World.WorldInfo Info = new World.WorldInfo();
            public List<BlockDef> BlockDefs = new List<BlockDef>();
            public List<Block> BlockIndexCache = new List<Block>();
        }

        protected ushort MakeBlock(int surface, Block.Geometries shape, Directions dir = Directions.None, byte minD = Block.ZeroHeight, byte maxD = Block.FullHeight, bool fluid = false)
        {
            if (minD == Block.ZeroHeight && shape == Block.Geometries.LowerRamp)
                shape = Block.Geometries.FullRamp;

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

        public int Dirt = BlockDef.EmptyID;
        public int Stone = BlockDef.EmptyID;
        public int DeepStone = BlockDef.EmptyID;
        public int Grass = BlockDef.EmptyID;
        public int Water = BlockDef.EmptyID;

        public int Blue = BlockDef.EmptyID;
        public int Red = BlockDef.EmptyID;
        public int Tan = BlockDef.EmptyID;

        public ushort SolidStone = 0;
        public ushort SolidDirt = 0;
        public ushort SolidGrass = 0;
        public ushort FluidWater = 0;
        public ushort SolidDeepStone = 0;

        protected MessagePackSerializer<WorldHeader> HeaderSerializer = MessagePackSerializer.Get<WorldHeader>();
        protected void InitWorld(DirectoryInfo dir)
        {
            World.Clear();

            HeaderFile = new FileInfo(Path.Combine(dir.FullName, "world.header"));
            if (HeaderFile.Exists)
            {
                var fs = HeaderFile.OpenRead();
                var header = HeaderSerializer.Unpack(fs);
                fs.Close();
                World.Info = header.Info;
                World.BlockDefs = header.BlockDefs;
                World.BlockIndexCache = header.BlockIndexCache;
                World.BindAllTextures();
            }
            else
            {
                World.AddTexture(("data/textures/dirt.png"));           //0
                World.AddTexture(("data/textures/stone.png"));          //1
                World.AddTexture(("data/textures/grass_top.png"));      //2
                World.AddTexture(("data/textures/dirt_grass.png"));     //3
                World.AddTexture(("data/textures/water_trans.xml"));    //4
                World.AddTexture(("data/textures/cotton_blue.png"));    //5
                World.AddTexture(("data/textures/cotton_red.png"));     //6
                World.AddTexture(("data/textures/cotton_tan.png"));     //7
                World.AddTexture(("data/textures/greystone.png"));      //8

                Dirt = World.AddBlockDef(new BlockDef("Dirt", 0));
                Stone = World.AddBlockDef(new BlockDef("Stone", 1));
                DeepStone = World.AddBlockDef(new BlockDef("DeepStone", 8));
                Grass = World.AddBlockDef(new BlockDef("Grass", 2, 3, 0));
                Water = World.AddBlockDef(new BlockDef("Water", 4));

                Blue = World.AddBlockDef(new BlockDef("Blue", 5));
                Red = World.AddBlockDef(new BlockDef("Red", 6));
                Tan = World.AddBlockDef(new BlockDef("Tan", 7));

                World.BlockDefs[Water].Transperant = true;

                SolidStone = MakeBlock(Stone, Block.Geometries.Solid);
                SolidDirt = MakeBlock(Dirt, Block.Geometries.Solid);
                SolidGrass = MakeBlock(Grass, Block.Geometries.Solid);
                SolidDeepStone = MakeBlock(DeepStone, Block.Geometries.Solid);
                FluidWater = MakeBlock(Water, Block.Geometries.Solid, Directions.None, Block.ZeroHeight, Block.OpenFluidHeight, true);
            }

            WorldInited?.Invoke(this, EventArgs.Empty);
        }

        protected void CaptureWorldEdits()
        {
            World.TextureAdded += World_TextureAdded;
            World.BlockDefAdded += World_BlockDefAdded;
            World.BlockIndexAdded += World_BlockIndexAdded;

            World.ClusterAdded += WorldClusterAdded;
            World.ClusterDirty += WorldClusterDirty;
        }

        protected virtual void WorldClusterDirty(ClusterPos pos)
        {
            throw new NotImplementedException();
        }

        protected virtual void WorldClusterAdded(ClusterPos pos)
        {
            throw new NotImplementedException();
        }

        private void SaveHeaderAsync()
        {
            WorldHeader header = new WorldHeader();
            header.BlockDefs = World.BlockDefs;
            header.BlockIndexCache = World.BlockIndexCache;
            header.Info = World.Info;

            var fs = HeaderFile.Open(FileMode.Truncate, FileAccess.Write);
            HeaderSerializer.PackAsync(fs, header).ContinueWith(new Action<Task>(x => fs.Close()));
        }

        private void World_BlockIndexAdded(int index)
        {
            SaveHeaderAsync();
        }

        private void World_BlockDefAdded(int index)
        {
            SaveHeaderAsync();
        }

        private void World_TextureAdded(int index)
        {
            SaveHeaderAsync();
        }
    }
}
