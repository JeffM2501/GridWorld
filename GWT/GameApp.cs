using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

namespace GridWorld.Test
{
    internal class GameApp : Application
    {
        public GameApp(ApplicationOptions options = null) : base(options) { }

        public Scene RootScene = null;
        public static Node CameraNode = null;

        GridWorld.World Map = new World();


        protected override void Start()
        {
            base.Start();

            Input.SetMouseMode(MouseMode.Absolute);
            Input.SetMouseVisible(true);

            Renderer.ShadowQuality = ShadowQuality.PcfN24Bit;
            Renderer.ShadowMapSize = (4) * 1024;

            SetupScene();
        }

        public void SetupScene()
        {
            this.Renderer.TextureFilterMode = TextureFilterMode.Nearest;
            this.Renderer.TextureAnisotropy = 8;

            RootScene = new Scene();
            RootScene.CreateComponent<Octree>();

            RootScene.CreateComponent<DebugRenderer>();


//             var node = RootScene.CreateChild("test");
//             var model = node.CreateComponent<StaticModel>();
//             model.Model = Urho.CoreAssets.Models.Box;
//             model.Material = Urho.CoreAssets.Materials.DefaultGrey;
// 
//             node.Position = new Vector3(0, 10, 0);
//             node.Rotation = new Quaternion(0, 45, 0);
//             node.Scale = new Vector3(10, 10, 10);

            string skyboxName = "hills.xml";
            if (skyboxName != string.Empty && ResourceCache.GetMaterial("skyboxes/" + skyboxName, false) != null)
            {
                var skybox = RootScene.CreateChild("Sky").CreateComponent<Skybox>();
                skybox.Model = Urho.CoreAssets.Models.Box;
                skybox.Material = ResourceCache.GetMaterial("skyboxes/" + skyboxName, false);
            }

            SetupDisplay();

            new WorldBuilder.FlatBuilder().Build(string.Empty, null, Map);
            //new WorldBuilder.FlatBuilder().BuildSimple(string.Empty, null, Map);


            float d = Map.DropDepth(CameraNode.Position.X, CameraNode.Position.Z);
            if (d != float.MinValue)
                CameraNode.Position = new Vector3(CameraNode.Position.X, d + 1, CameraNode.Position.Z);

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
                var clusterData = cluster.Value;

                var clusterNode = RootScene.CreateChild("test");
                clusterNode.Position = new Vector3(cluster.Key.H, 0, cluster.Key.V);
                clusterNode.SetScale(1);
                ClusterGeometry.BuildGeometry(Map, clusterData);

                if (clusterData.Geometry != null)
                {
                    var geos = clusterData.Geometry.BindToUrhoGeo();
                   
                    var model = clusterNode.CreateComponent<StaticModel>();

                    var meshGroup = new Model();
                    int index = 0;
                    meshGroup.NumGeometries = (uint)geos.Count;

                    foreach (var geo in geos)
                    {
                        meshGroup.SetGeometry((uint)index, 0, geo.Item1);
                        meshGroup.SetGeometryCenter((uint)index, Vector3.Zero);
                        index++;
                    }

                    meshGroup.BoundingBox = new BoundingBox(new Vector3(0,0,0),new Vector3(Cluster.HVSize,Cluster.DSize,Cluster.HVSize));
                    model.Model = meshGroup;

                    index = 0;
                    foreach (var geo in geos)
                    {
                        model.SetMaterial((uint)index, Map.Info.Textures[geo.Item2].RuntimeMat);
                        index++;
                    }
                    model.CastShadows = true;
                }
                else
                {
                    cluster.Value.DoForEachBlock((pos, block) =>
                    {
                        if (block.DefID >= 0)
                        {
                            var node = clusterNode.CreateChild("test");
                            var model = node.CreateComponent<StaticModel>();
                            model.Model = Urho.CoreAssets.Models.Box;
                            model.Material = Urho.CoreAssets.Materials.DefaultGrey;

                            node.Position = pos;
                            node.Scale = new Vector3(1, 1, 1);
                        }
                    });
                }
            }
        }

        public void SetupDisplay()
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
            zone.AmbientColor = new Color(0.25f, 0.25f, 0.25f, 1);

            var graphics = Graphics;
            camera.OrthoSize = (float)graphics.Height * PixelSize;

            Node lightNode = RootScene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3(0.6f, -1.0f, 0.8f));
            Light light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.CastShadows = true;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            // Set cascade splits at 10, 50 and 200 world units, fade shadows out at 80% of maximum shadow distance
            light.ShadowCascade = new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f);


            Renderer.SetViewport(0, new Viewport(Context, RootScene, camera, null));

        }

        protected override void OnUpdate(float timeStep)
        {
            float moveSpeed = 10;

            if (Input.GetKeyDown(Key.W))
                CameraNode.Translate(CameraNode.Direction * (timeStep * moveSpeed), TransformSpace.World);
            else if (Input.GetKeyDown(Key.S))
                CameraNode.Translate(CameraNode.Direction * (timeStep * -moveSpeed), TransformSpace.World);

            if (Input.GetKeyDown(Key.R))
                CameraNode.Translate(CameraNode.Up * timeStep * moveSpeed, TransformSpace.World);
            else if (Input.GetKeyDown(Key.V))
                CameraNode.Translate(CameraNode.Up * timeStep * -moveSpeed, TransformSpace.World);


            if (Input.GetKeyDown(Key.A))
                CameraNode.Translate(CameraNode.Right * timeStep * -moveSpeed, TransformSpace.World);
            else if (Input.GetKeyDown(Key.D))
                CameraNode.Translate(CameraNode.Right * timeStep * moveSpeed, TransformSpace.World);


            float rotSpeed = 60;
            float rotDelta = 0;
            if (Input.GetKeyDown(Key.Q))
                rotDelta = timeStep * -rotSpeed;
            else if (Input.GetKeyDown(Key.E))
                rotDelta = timeStep * rotSpeed;

            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitY, rotDelta);
            CameraNode.Rotate(q);

            rotDelta = 0;
            if (Input.GetKeyDown(Key.T))
                rotDelta = timeStep * -rotSpeed;
            else if (Input.GetKeyDown(Key.B))
                rotDelta = timeStep * rotSpeed;

            q = Quaternion.FromAxisAngle(Vector3.UnitX, rotDelta);
            CameraNode.Rotate(q);

        }
    }
}
