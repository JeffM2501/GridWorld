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

using Urho;
using Urho.Gui;

using GridWorld.Test.Components;
using GridWorld.Test.Geometry;

namespace GridWorld.Test
{
    internal class GameApp : Application
    {
        private bool Exiting = false;
        public event EventHandler ApplicationExiting = null;

        public GameApp(ApplicationOptions options = null) : base(options) { }

        public Scene RootScene = null;
        public static Node CameraNode = null;

        public static Color AmbientColor = new Color(0.35f, 0.35f, 0.35f);

        public Node PlayerNode = null;

        public UIElement AvatarStatusElement = null;

        protected override void Start()
        {
            base.Start();

            Input.SetMouseMode(MouseMode.Absolute);
            Input.SetMouseVisible(true);

            Input.ExitRequested += Input_ExitRequested;

            SetupRenderer();
  
            SetupScene();

            SetupUI();
        }

        private void Input_ExitRequested(ExitRequestedEventArgs obj)
        {
            PreExit();
        }

        protected void PreExit()
        {
            Exiting = true;
            ApplicationExiting?.Invoke(this, EventArgs.Empty);

//             if (Config.Current != null && Config.Current.WinType == Config.WindowTypes.Window)
//             {
//                 Config.Current.WindowBounds = new System.Drawing.Rectangle(Graphics.WindowPosition.X, Graphics.WindowPosition.Y, Graphics.Width, Graphics.Height);
//             }
        }

        protected void DoExit()
        {
            PreExit();
            Exit();
        }


        protected Text PostionGUI = null;

        public void SetupUI()
        {
            AvatarStatusElement = new UIElement();
            UI.Root.AddChild(AvatarStatusElement);

            AvatarStatusElement.EnableAnchor = true;
            AvatarStatusElement.SetMaxAnchor(0.125f, 0.125f);
            AvatarStatusElement.SetMinAnchor(0, 0);

            var text = new Text();
            text.Value = "Walking";
            text.HorizontalAlignment = HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Top;
            text.SetFont(ResourceCache.GetFont("fonts/Open_Sans/OpenSans-Regular.ttf"), 14);
            text.Position = new IntVector2(10,0);
            text.SetColor(Color.Gray);
            text.SetMaxAnchor(1, 1);
            text.SetMinAnchor(0, 0);
            AvatarStatusElement.AddChild(text);

            PlayerNode.GetComponent<PlayerAvatarController>().FlightStatusChanged += new EventHandler((o, e) => text.Value = (o as PlayerAvatarController).Flying ? "Flying" : "Walking");

            PostionGUI = new Text();
            PostionGUI.Value = "X0 Y0 Z0";
            PostionGUI.HorizontalAlignment = HorizontalAlignment.Left;
            PostionGUI.VerticalAlignment = VerticalAlignment.Top;
            PostionGUI.SetFont(ResourceCache.GetFont("fonts/Open_Sans/OpenSans-Regular.ttf"), 14);
            PostionGUI.Position = new IntVector2(10, 20);
            PostionGUI.SetColor(Color.Gray);
            PostionGUI.SetMaxAnchor(1, 1);
            PostionGUI.SetMinAnchor(0, 0);
            AvatarStatusElement.AddChild(PostionGUI);

           var debug = new MonoDebugHud(this);
           debug.Show(Color.Gray);
        }

        protected WorldBuilder.FlatBuilder PerlinBuilder = null;

        bool GenerateTerrain = false;

        public void SetupScene()
        {
            this.Renderer.TextureFilterMode = TextureFilterMode.Nearest;
            this.Renderer.TextureAnisotropy = 8;

            RootScene = new Scene();
            RootScene.CreateComponent<Octree>();

            RootScene.CreateComponent<DebugRenderer>();

            PerlinBuilder = new WorldBuilder.FlatBuilder();
            if (GenerateTerrain)
            {
                PerlinBuilder.BuildPerlin(string.Empty, null);
            }
            else
                PerlinBuilder.Build(string.Empty, null);

            GeoLoadManager.NeedCluster += PerlinBuilder.EnqueCluster;
            SetupCamera();

            PlayerNode = RootScene.CreateChild("local_player");
            PlayerNode.AddComponent(new PlayerAvatarController() {CameraNode = CameraNode.GetComponent<Camera>() });

            var depthTester = RootScene.CreateChild("depth_tester");
            depthTester.AddComponent(new DepthTester());

            SetupSky(World.Info.SunPosition.X, World.Info.SunPosition.Y, World.Info.SunPosition.Z);

            float d = World.DropDepth(PlayerNode.Position.X, PlayerNode.Position.Z);
            if (d != float.MinValue)
                PlayerNode.Position = new Vector3(PlayerNode.Position.X, d, PlayerNode.Position.Z);

            foreach (var texture in World.Info.Textures)
            {
                if (texture.RuntimeMat == null)
                {
                    Material mat = null;
                    if (System.IO.Path.GetExtension(texture.FileName).ToUpperInvariant() == ".XML")
                        mat = ResourceCache.GetMaterial(texture.FileName);
                    else
                        mat = Material.FromImage(texture.FileName);

                    texture.RuntimeMat = mat;
                }
            }

            foreach (var cluster in World.Clusters)
            {
                var clusterNode = RootScene.CreateChild("Cluster" + cluster.Key.ToString());
                clusterNode.AddComponent(new ClusterInfo(cluster.Value));
            }

            // start the geo generation process
            GeoLoadManager.UpdateGeoForPosition(CameraNode.WorldPosition);
        }

        public void SetupSky(float sunPosX, float sunPosY, float sunPosZ)
        {
            Node lightNode = RootScene.CreateChild("SunLight");

            Vector3 sunVec = new Vector3(-sunPosX, -sunPosY, -sunPosZ);
            sunVec.Normalize();

            lightNode.SetDirection(sunVec);
            Light light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.CastShadows = true;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            // Set cascade splits at 10, 50 and 200 world units, fade shadows out at 80% of maximum shadow distance
            light.ShadowCascade = new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f);


            string skyboxName = "clouds.xml";
            if (skyboxName != string.Empty && ResourceCache.GetMaterial("skyboxes/" + skyboxName, false) != null)
            {
                var skybox = RootScene.CreateChild("Sky").CreateComponent<Skybox>();
                skybox.Model = Urho.CoreAssets.Models.Box;
                skybox.Material = ResourceCache.GetMaterial("skyboxes/" + skyboxName, false);
            }
            var sun = RootScene.CreateChild("Sun");
            sun.AddComponent(new Sun() { CameraNode = CameraNode});
        }

        public void SetupCamera()
        {
            CameraNode = RootScene.CreateChild("Camera");
            CameraNode.Position = (new Vector3(0.0f, 25, -10));
            // CameraNode.Rotation = new Quaternion(90, 0, 0);
            Camera camera = CameraNode.CreateComponent<Camera>();
            camera.Orthographic = false;
            camera.NearClip = 0.1f;
            camera.FarClip = 5000;

            var zone = CameraNode.CreateComponent<Zone>();
            zone.SetBoundingBox(new BoundingBox(camera.Frustum));
            zone.FogColor = Color.Transparent;
            zone.FogStart = 50000;
            zone.FogEnd = 50000;
            zone.AmbientColor = AmbientColor;

            var graphics = Graphics;
            camera.OrthoSize = (float)graphics.Height * PixelSize;

            Renderer.SetViewport(0, new Viewport(Context, RootScene, camera, null));
        }

        public void SetupRenderer()
        {
            int shadowMapBase = 16;
            Renderer.ShadowQuality = ShadowQuality.PcfN24Bit;
            Renderer.ShadowMapSize = (shadowMapBase) * 1024;
        }

        protected override void OnUpdate(float timeStep)
        {
            if (Exiting)
                return;

            GeoLoadManager.CheckOrigin(PlayerNode.WorldPosition);

            var newNodes = PerlinBuilder.PopNewClusters();
            if (newNodes != null)
            {
                foreach (var newcluster in newNodes)
                {
                    var clusterNode = RootScene.CreateChild("Cluster" + newcluster.Origin.ToString());
                    clusterNode.AddComponent(new ClusterInfo(newcluster));
                }
            }

            Geometry.LoadLimiter.UpdateFrame();
            GeoLoadManager.UpdateGeoForPosition(CameraNode.WorldPosition);

            PostionGUI.Value = CameraNode.WorldPosition.ToString(6);
//             var cluster = World.ClusterFromPosition(CameraNode.WorldPosition);
//             if (cluster != null)
//             {
//                 PostionGUI.Value += " CO H" + cluster.Origin.H.ToString() + " V" + cluster.Origin.V.ToString();
// 
//                 PostionGUI.Value += " BO H" + ((int)(CameraNode.WorldPosition.X - cluster.Origin.H)).ToString() + " V" + ((int)(CameraNode.WorldPosition.Z - cluster.Origin.V)).ToString();
//             }

            PostionGUI.Value += "Origin H" + GeoLoadManager.CurrentOrigin.H.ToString() + " V" + GeoLoadManager.CurrentOrigin.V.ToString();

            base.OnUpdate(timeStep);
        }
    }
}
