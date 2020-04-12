using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

namespace GridWorld.Test.Components
{
    public class DepthTester : LogicComponent
    {
        public Material NormalMaterial = null;
        public Material WarningMaterial = null;
        public Material ErrorMaterial = null;

        bool first = true;

        public List<StaticModel> Models = new List<StaticModel>();


        public DepthTester()
        {
            ReceiveSceneUpdates = true;
        }

        public override void OnAttachedToNode(Node node)
        {
            base.OnAttachedToNode(node);

            NormalMaterial = Material.FromColor(Color.Blue);
            WarningMaterial = Material.FromColor(Color.Yellow);
            ErrorMaterial = Material.FromColor(Color.Red);

            Node tip = node.CreateChild("tip");

            var tipMesh = tip.CreateComponent<StaticModel>();
            tipMesh.Model = Urho.CoreAssets.Models.Cone;
            tipMesh.Material = NormalMaterial;
            tip.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, 180);
            tip.Position = new Vector3(0, 0.5f, 0);

            Models.Add(tipMesh);

            Node shaft = node.CreateChild("shaft");
            var shaftMesh = shaft.CreateComponent<StaticModel>();
            shaftMesh.Model = Urho.CoreAssets.Models.Cylinder;
            shaftMesh.Material = NormalMaterial;
            shaft.Scale = new Vector3(0.25f, 1, 0.25f);
            shaft.Position = new Vector3(0, 1.5f, 0);
            Models.Add(shaftMesh);
        }

        protected override void OnUpdate(float timeStep)
        {
            base.OnUpdate(timeStep);

            float moveSpeed = 5;

            if (Application.Input.GetKeyDown(Key.Left))
                Node.Translate(Node.Right * timeStep * -moveSpeed, TransformSpace.World);
            else if (Application.Input.GetKeyDown(Key.Right))
                Node.Translate(Node.Right * timeStep * moveSpeed, TransformSpace.World);

            if (Application.Input.GetKeyDown(Key.Up))
                Node.Translate(Node.Direction * (timeStep * moveSpeed), TransformSpace.World);
            else if (Application.Input.GetKeyDown(Key.Down))
                Node.Translate(Node.Direction * (timeStep * -moveSpeed), TransformSpace.World);

            bool good = true;
            bool warn = false;
            try
            {
                float d = World.DropDepth(Node.Position.X, Node.Position.Z);
                if (d == float.MinValue)
                    good = false;
                else
                {
                    if (!first && Math.Abs(d - Node.Position.Y) > 10)
                        warn = true;
                    else
                        Node.Position = new Vector3(Node.Position.X, d, Node.Position.Z);
                }
            }
            catch (Exception)
            {

                good = false;
            }
           
            foreach (var mesh in Models)
            {
                if (warn)
                    mesh.SetMaterial(WarningMaterial);
                else if (good)
                    mesh.SetMaterial(NormalMaterial);
                else
                    mesh.SetMaterial(ErrorMaterial);
            }

            first = false;
        }
    }
}
