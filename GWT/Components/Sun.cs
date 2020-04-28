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
