﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;
using Urho.Gui;

using GridWorld.Test.Components;

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

        GridWorld.World Map = new World();

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

        }

        public void SetupScene()
        {
            this.Renderer.TextureFilterMode = TextureFilterMode.Nearest;
            this.Renderer.TextureAnisotropy = 8;

            RootScene = new Scene();
            RootScene.CreateComponent<Octree>();

            RootScene.CreateComponent<DebugRenderer>();

            new WorldBuilder.FlatBuilder().Build(string.Empty, null, Map);
            //new WorldBuilder.FlatBuilder().BuildSimple(string.Empty, null, Map);

            SetupCamera();

            PlayerNode = RootScene.CreateChild("local_player");
            PlayerNode.AddComponent(new PlayerAvatarController() { TheWorld = Map, CameraNode = CameraNode.GetComponent<Camera>() });

            var depthTester = RootScene.CreateChild("depth_tester");
            depthTester.AddComponent(new DepthTester() { TheWorld = Map });

            SetupSky(Map.Info.SunPosition.X, Map.Info.SunPosition.Y, Map.Info.SunPosition.Z);

            float d = Map.DropDepth(PlayerNode.Position.X, PlayerNode.Position.Z);
            if (d != float.MinValue)
                PlayerNode.Position = new Vector3(PlayerNode.Position.X, d, PlayerNode.Position.Z);

            foreach (var texture in Map.Info.Textures)
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

            foreach (var cluster in Map.Clusters)
            {
                var clusterNode = RootScene.CreateChild("Cluster" + cluster.Key.ToString());
                clusterNode.AddComponent(new ClusterInfo(cluster.Value, Map));
            }
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

            base.OnUpdate(timeStep);
        }
    }
}
