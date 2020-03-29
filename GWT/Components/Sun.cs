using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

namespace GridWorld.Test.Components
{
    public class Sun : LogicComponent
    {
        public Node CameraNode = null;
        public Vector3 SunPostion = new Vector3(100, 100, 100);
        public float SunScale = 5;

        protected Node SunNode = null;

        public Sun()
        {
            ReceiveSceneUpdates = true;
        }

        public override void OnAttachedToNode(Node node)
        {
            base.OnAttachedToNode(node);

            SunNode = Node.CreateChild("SunBillboard");
            SunNode.Position = SunPostion;
            //             var sunModel = SunNode.CreateComponent<StaticModel>();
            //             sunModel.Model = Urho.CoreAssets.Models.Sphere;
            //             sunModel.Material = Urho.CoreAssets.Materials.DefaultGrey;
            //             SunNode.SetScale(SunScale);

            var billboardObject = SunNode.CreateComponent<BillboardSet>();
            billboardObject.NumBillboards = 1;
            billboardObject.Material = Application.ResourceCache.GetMaterial("data/Sprites/sun.xml");
            billboardObject.Sorted = true;

            var bb = billboardObject.GetBillboardSafe(0);
            bb.Position = Vector3.Zero;
            bb.Size = new Vector2(SunScale, SunScale);
            bb.Rotation = 0;
            bb.Enabled = true;

            // After modifying the billboards, they need to be "commited" so that the BillboardSet updates its internals
            billboardObject.Commit();
        }

        protected override void OnUpdate(float timeStep)
        {
            base.OnUpdate(timeStep);
            Node.SetWorldPosition(CameraNode.WorldPosition);
        }
    }
}
