using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

namespace GridWorld.Test.Components
{
    public class PlayerAvatarController : LogicComponent
    {
        public Camera CameraNode = null;

        public Node PivotNode = null;

        public bool Flying = false;

        public event EventHandler FlightStatusChanged = null;

        public PlayerAvatarController()
        {
            ReceiveSceneUpdates = true;
        }

        public override void OnAttachedToNode(Node node)
        {
            base.OnAttachedToNode(node);

            var bottomNode = Node.CreateChild("bottom");
            var bottomModel = bottomNode.CreateComponent<StaticModel>();
            bottomModel.Model = Urho.CoreAssets.Models.Sphere;
            bottomModel.Material = Urho.CoreAssets.Materials.DefaultGrey;
            bottomNode.Scale = new Vector3(0.25f, 0.25f, 0.25f);
            bottomNode.Position = new Vector3(0, 0.125f, 0);

            var midNode = Node.CreateChild("mid");
            var midModel = midNode.CreateComponent<StaticModel>();
            midModel.Model = Urho.CoreAssets.Models.Cylinder;
            midModel.Material = Urho.CoreAssets.Materials.DefaultGrey;
            midNode.Scale = new Vector3(0.25f, 1.25f, 0.25f);
            midNode.Position = new Vector3(0, 0.75f, 0);

            var topNode = Node.CreateChild("top");
            var topModel = topNode.CreateComponent<StaticModel>();
            topModel.Model = Urho.CoreAssets.Models.Sphere;
            topModel.Material = Urho.CoreAssets.Materials.DefaultGrey;
            topNode.Scale = new Vector3(0.75f, 0.75f, 0.75f);
            topNode.Position = new Vector3(0, 1.65f, 0);

            PivotNode = topNode.CreateChild("pivot");

            CameraNode.Node.Parent = PivotNode;
            CameraNode.Node.Position = new Vector3(0, 0, 0);
        }

        public float MouseXSensitivity = 0.5f;
        public float MouseYSensitivity = 0.5f;
        public float MouseZSensitivity = 0.125f;

        protected override void OnUpdate(float timeStep)
        {
            base.OnUpdate(timeStep);

            Vector3 oldPos = Node.Position;

            float moveSpeed = 20;
            if (Application.Input.GetKeyDown(Key.Shift))
                moveSpeed *= 5;

            if (Application.Input.GetKeyPress(Key.F1))
            {
                Flying = !Flying;
                FlightStatusChanged?.Invoke(this, EventArgs.Empty);
            }

            float mouseXValue = Application.Input.MouseMove.X * MouseXSensitivity;
            float mouseYValue = Application.Input.MouseMove.Y * MouseYSensitivity;
            float mouseZValue = Application.Input.MouseMoveWheel * MouseZSensitivity;

            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitY, mouseXValue);
            Node.Rotate(q);

            q = Quaternion.FromAxisAngle(Vector3.UnitX, mouseYValue);
            PivotNode.Rotate(q);

            if (Application.Input.GetKeyDown(Key.A))
                Node.Translate(Node.Right * timeStep * -moveSpeed, TransformSpace.World);
            else if (Application.Input.GetKeyDown(Key.D))
                Node.Translate(Node.Right * timeStep * moveSpeed, TransformSpace.World);

            var angles = PivotNode.Rotation.ToEulerAngles();
            if (angles.X < -80)
                PivotNode.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, -80);
            else if (angles.X > 45)
                PivotNode.Rotation = Quaternion.FromAxisAngle(Vector3.UnitX, 45);


            if (Application.Input.GetKeyDown(Key.W))
                Node.Translate(Node.Direction * (timeStep * moveSpeed), TransformSpace.World);
            else if (Application.Input.GetKeyDown(Key.S))
                Node.Translate(Node.Direction * (timeStep * -moveSpeed), TransformSpace.World);


            if (Flying)
            {
                if (Application.Input.GetKeyDown(Key.Space))
                    Node.Translate(Node.Up * (timeStep * moveSpeed), TransformSpace.World);
                if (Application.Input.GetKeyDown(Key.Ctrl))
                    Node.Translate(Node.Up * (timeStep * -moveSpeed), TransformSpace.World);
            }

            if (mouseZValue != 0)
                CameraNode.Node.Translate(CameraNode.Node.Direction * mouseZValue, TransformSpace.Local);

            if (Application.Input.GetMouseButtonDown(MouseButton.Middle))
                CameraNode.Node.Position = new Vector3(0, 0, 0);

            if (!Flying)
            {
                float d = World.DropDepth(Node.Position.X, Node.Position.Z);
                if (d != float.MinValue)
                {
                    float actualD = Node.Position.Y;
                    if (actualD > d)
                    {
                        // going down
                        actualD -= timeStep * 8;
                        if (actualD < d)
                            actualD = d;
                    }
                    else if (actualD < d)
                    {
                        if (d - actualD > 2f)
                        {
                            Node.Position = oldPos;
                            return;
                        }
                        // going up
                        actualD += timeStep * 20;
                        if (actualD > d)
                            actualD = d;
                    }


                    Node.Position = new Vector3(Node.Position.X, actualD, Node.Position.Z);
                }
                    
            }
        }
    }
}
