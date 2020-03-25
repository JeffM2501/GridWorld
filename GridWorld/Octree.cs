using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Urho;

namespace GridWorld
{
    public interface IOctreeObject
    {
        BoundingBox GetOctreeBounds();
    }

    public enum ContainmentType
    {
        Disjoint,
        Contains,
        Intersects,
    }

    public enum PlaneIntersectionType
    {
        Front,
        Back,
        Intersecting,
    }

    public static class GeoUtils
    {
        public static ContainmentType Contains(this BoundingBox thisBox, BoundingBox box)
        {
            //test if all corner is in the same side of a face by just checking min and max
            if (box.Max.X < thisBox.Min.X
                || box.Min.X > thisBox.Max.X
                || box.Max.Y < thisBox.Min.Y
                || box.Min.Y > thisBox.Max.Y
                || box.Max.Z < thisBox.Min.Z
                || box.Min.Z > thisBox.Max.Z)
                return ContainmentType.Disjoint;

            if (box.Min.X >= thisBox.Min.X
                && box.Max.X <= thisBox.Max.X
                && box.Min.Y >= thisBox.Min.Y
                && box.Max.Y <= thisBox.Max.Y
                && box.Min.Z >= thisBox.Min.Z
                && box.Max.Z <= thisBox.Max.Z)
                return ContainmentType.Contains;


            return ContainmentType.Intersects;
        }

        public static Vector3[] GetCorners(this BoundingBox thisBox)
        {
            Vector3[] corners = new Vector3[8];
            corners[0] = thisBox.Min;
            corners[1] = new Vector3(thisBox.Max.X, thisBox.Min.Y, thisBox.Min.Z);
            corners[2] = new Vector3(thisBox.Min.X, thisBox.Max.Y, thisBox.Min.Z);
            corners[3] = new Vector3(thisBox.Max.X, thisBox.Max.Y, thisBox.Min.Z);

            corners[4] = thisBox.Max;
            corners[5] = new Vector3(thisBox.Min.X, thisBox.Max.Y, thisBox.Max.Z);
            corners[6] = new Vector3(thisBox.Max.X, thisBox.Min.Y, thisBox.Max.Z);
            corners[7] = new Vector3(thisBox.Min.X, thisBox.Min.Y, thisBox.Max.Z);

            return corners;
        }

        public static bool Intersects(this Frustum thisFrustrum, BoundingBox box)
        {
            foreach (Plane plane in thisFrustrum.Planes)
            {
                if ((plane.Normal.X * box.Min.X) + (plane.Normal.Y * box.Min.Y) + (plane.Normal.Z * box.Min.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Max.X) + (plane.Normal.Y * box.Min.Y) + (plane.Normal.Z * box.Min.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Max.X) + (plane.Normal.Y * box.Min.Y) + (plane.Normal.Z * box.Max.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Min.X) + (plane.Normal.Y * box.Min.Y) + (plane.Normal.Z * box.Max.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Min.X) + (plane.Normal.Y * box.Max.Y) + (plane.Normal.Z * box.Min.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Max.X) + (plane.Normal.Y * box.Max.Y) + (plane.Normal.Z * box.Min.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Max.X) + (plane.Normal.Y * box.Max.Y) + (plane.Normal.Z * box.Max.Z) + plane.D > 0)
                    continue;

                if ((plane.Normal.X * box.Min.X) + (plane.Normal.Y * box.Max.Y) + (plane.Normal.Z * box.Max.Z) + plane.D > 0)
                    continue;

                // all points are behind the one plane so they can't be inside any other plane
                return false;
            }

            return true;
        }

        public static ContainmentType Contains(this Frustum thisFrustrum, BoundingBox box)
        {
            // FIXME: Is this a bug?
            // If the bounding box is of W * D * H = 0, then return disjoint
            if (box.Min == box.Max)
                return  ContainmentType.Disjoint;

            int i = 0;
            Vector3[] corners = box.GetCorners();

            // First we assume completely disjoint. So if we find a point that is contained, we break out of this loop
            for (i = 0; i < corners.Length; i++)
            {
                if (thisFrustrum.IsInside(corners[i]) == Intersection.Inside)
                    break;
            }

            if (i == corners.Length) // This means we checked all the corners and they were all disjoint
                return ContainmentType.Disjoint;

            if (i != 0)             // if i is not equal to zero, we can fastpath and say that this box intersects
                return ContainmentType.Intersects;

            // If we get here, it means the first (and only) point we checked was actually contained in the frustum.
            // So we assume that all other points will also be contained. If one of the points is disjoint, we can
            // exit immediately saying that the result is Intersects
            i++;
            for (; i < corners.Length; i++)
            {
                if (thisFrustrum.IsInside(corners[i]) != Intersection.Inside)
                    return ContainmentType.Intersects;
            }

            // If we get here, then we know all the points were actually contained, therefore result is Contains
            return ContainmentType.Contains;

        }

        internal static float PerpendicularDistance(ref Vector3 point, ref Plane plane)
        {
            // dist = (ax + by + cz + d) / sqrt(a*a + b*b + c*c)
            return (float)System.Math.Abs((plane.Normal.X * point.X +
                                           plane.Normal.Y * point.Y +
                                           plane.Normal.Z * point.Z) /
                                          System.Math.Sqrt(plane.Normal.X * plane.Normal.X +
                                                           plane.Normal.Y * plane.Normal.Y +
                                                           plane.Normal.Z * plane.Normal.Z));
        }

        public static float Distance(this Plane thisPlane, Vector3 point)
        {
            return PerpendicularDistance(ref point, ref thisPlane);
        }

        public static float InsersectionTolerance = 0.0001f;

        public static PlaneIntersectionType IntersectsPoint(this Plane thisPlane, Vector3 point)
        {
            Vector3 vec = thisPlane.Normal * thisPlane.D - point;

            float dot = Vector3.Dot(vec, thisPlane.Normal);

            if (dot < -InsersectionTolerance)
                return PlaneIntersectionType.Front;
            if (dot > InsersectionTolerance)
                return PlaneIntersectionType.Back;
            return PlaneIntersectionType.Intersecting;
        }
    }

    public class OctreeLeaf
    {
        [System.Xml.Serialization.XmlIgnoreAttribute]
        const int maxDepth = 40;

        [System.Xml.Serialization.XmlIgnoreAttribute]
        const bool doFastOut = true;

        [System.Xml.Serialization.XmlIgnoreAttribute]
        int maxObjects = 8;

        [System.Xml.Serialization.XmlIgnoreAttribute]
        public List<object> containedObjects = new List<object>();

        public List<OctreeLeaf> children = null;
        public BoundingBox bounds;

        public OctreeLeaf(BoundingBox containerBox)
        {
            bounds = containerBox;
        }

        public List<object> ContainedObjects
        {
            get { return containedObjects; }
            set { containedObjects = value; }
        }

        public List<OctreeLeaf> ChildLeaves
        {
            get { return children; }
        }

        public BoundingBox ContainerBox
        {
            get { return bounds; }
            set { bounds = value; }
        }

        public void FastRemove(object item)
        {
            if (ContainedObjects.Contains(item))
            {
                List<OctreeLeaf> toRemove = new List<OctreeLeaf>();

                ContainedObjects.Remove(item);
                foreach (OctreeLeaf leaf in ChildLeaves)
                {
                    leaf.FastRemove(item);
                    if (leaf.children == null || leaf.children.Count == 0)
                        toRemove.Add(leaf);
                }

                foreach (OctreeLeaf leaf in toRemove)
                    ChildLeaves.Remove(leaf);
            }
        }

        protected void Split()
        {
            if (children != null)
                return;

            Vector3 half = ContainerBox.Max - ContainerBox.Min;
            half *= 0.5f;
            Vector3 halfx = new Vector3(half.X, 0, 0);
            Vector3 halfy = new Vector3(0, half.Y, 0);
            Vector3 halfz = new Vector3(0, 0, half.Z);

            children = new List<OctreeLeaf>();

            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min, ContainerBox.Min + half)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfx, ContainerBox.Max - half + halfx)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfz, ContainerBox.Min + half + halfz)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfx + halfz, ContainerBox.Max - halfy)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfy, ContainerBox.Max - halfx - halfz)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfy + halfx, ContainerBox.Max - halfz)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + halfy + halfz, ContainerBox.Max - halfx)));
            ChildLeaves.Add(new OctreeLeaf(new BoundingBox(ContainerBox.Min + half, ContainerBox.Max)));
        }

        public void Distribute(int depth)
        {
            if (containedObjects.Count > maxObjects && depth <= maxDepth)
            {
                Split();
                for (int i = containedObjects.Count - 1; i >= 0; i--)// (OctreeObject item in containedObjects)
                {
                    object item = containedObjects[i];
                    foreach (OctreeLeaf leaf in ChildLeaves)
                    {
                        IOctreeObject o = item as IOctreeObject;
                        BoundingBox bounds = o.GetOctreeBounds();
                        if (leaf.ContainerBox.Contains(bounds) == ContainmentType.Contains)
                        {
                            leaf.ContainedObjects.Add(item);
                            containedObjects.Remove(item);
                            break;
                        }
                    }
                }

                depth++;
                foreach (OctreeLeaf leaf in ChildLeaves)
                    leaf.Distribute(depth);
                depth--;
            }
        }

        protected void FastAddChildren(List<object> objects)
        {
            foreach (object item in containedObjects)
                objects.Add(item);

            if (ChildLeaves != null)
            {
                foreach (OctreeLeaf leaf in ChildLeaves)
                    leaf.FastAddChildren(objects);
            }
        }

        public virtual void ObjectsInFrustum(List<object> objects, Frustum boundingFrustum)
        {
            // if the current box is totally contained in our leaf, then add me and all my kids
            if (doFastOut && boundingFrustum.Contains(ContainerBox) == ContainmentType.Contains)
                FastAddChildren(objects);
            else
            {
                // ok so we know that we are probably intersecting or outside
                foreach (object item in containedObjects) // add our straglers
                    objects.Add(item);

                if (ChildLeaves != null)
                {
                    foreach (OctreeLeaf leaf in ChildLeaves)
                    {
                        // if the child is totally in the volume then add it and it's kids
                        if (doFastOut && boundingFrustum.Contains(leaf.ContainerBox) == ContainmentType.Contains)
                            leaf.FastAddChildren(objects);
                        else
                        {
                            if (boundingFrustum.Intersects(leaf.ContainerBox))
                                leaf.ObjectsInFrustum(objects, boundingFrustum);
                        }

                    }
                }
            }
        }

        public virtual void ObjectsInBoundingBox(List<object> objects, BoundingBox boundingBox)
        {
            // if the current box is totally contained in our leaf, then add me and all my kids
            if (boundingBox.Contains(ContainerBox) == ContainmentType.Contains)
                FastAddChildren(objects);
            else
            {
                // ok so we know that we are probably intersecting or outside
                foreach (object item in containedObjects) // add our straglers
                    objects.Add(item);

                if (ChildLeaves != null)
                {
                    foreach (OctreeLeaf leaf in ChildLeaves)
                    {
                        // see if any of the sub boxes intesect our frustum
                        if (leaf.ContainerBox.IsInside(boundingBox) == Intersection.Intersects)
                            leaf.ObjectsInBoundingBox(objects, boundingBox);
                    }
                }
            }
        }

        public virtual void ObjectsInBoundingSphere(List<object> objects, SphereShape boundingSphere)
        {
            // if the current box is totally contained in our leaf, then add me and all my kids
            if (boundingSphere.IsInside(ContainerBox) == Intersection.Inside)
                FastAddChildren(objects);
            else
            {
                // ok so we know that we are probably intersecting or outside
                foreach (object item in containedObjects) // add our straglers
                    objects.Add(item);

                if (ChildLeaves != null)
                {
                    foreach (OctreeLeaf leaf in ChildLeaves)
                    {
                        // see if any of the sub boxes intesect our frustum
                        if (boundingSphere.IsInside(leaf.ContainerBox) == Intersection.Intersects)
                            leaf.ObjectsInBoundingSphere(objects, boundingSphere);
                    }
                }
            }
        }

        protected ContainmentType testBoxInFrustum(BoundingBox extents, Frustum frustum)
        {
            // TODO - use a sphere vs. cone test first?

            Vector3 inside;  // inside point  (assuming partial)
            Vector3 outside; // outside point (assuming partial)
            float len = 0;
            ContainmentType result = ContainmentType.Contains;

            foreach (Plane plane in frustum.Planes)
            {
                // setup the inside/outside corners
                // this can be determined easily based
                // on the normal vector for the plane
                if (plane.Normal.X > 0.0f)
                {
                    inside.X = extents.Max.X;
                    outside.X = extents.Min.X;
                }
                else
                {
                    inside.X = extents.Min.X;
                    outside.X = extents.Max.X;
                }

                if (plane.Normal.Y > 0.0f)
                {
                    inside.Y = extents.Max.Y;
                    outside.Y = extents.Min.Y;
                }
                else
                {
                    inside.Y = extents.Min.Y;
                    outside.Y = extents.Max.Y;
                }

                if (plane.Normal.Z > 0.0f)
                {
                    inside.Z = extents.Max.Z;
                    outside.Z = extents.Min.Z;
                }
                else
                {
                    inside.Z = extents.Min.Z;
                    outside.Z = extents.Max.Z;
                }

                // check the inside length
                len = plane.Distance(inside);
                if (len < -1.0f)
                    return ContainmentType.Disjoint; // box is fully outside the frustum

                // check the outside length
                len = plane.Distance(outside);
                if (len < -1.0f)
                    result = ContainmentType.Intersects; // partial containment at best
            }

            return result;
        }
    }

    public class OctreeRoot : OctreeLeaf
    {
        public OctreeRoot()
            : base(new BoundingBox())
        {
        }

        public void Bounds()
        {
            foreach (IOctreeObject item in ContainedObjects)
            {
                ContainerBox.Merge(item.GetOctreeBounds());
            }
        }

        public virtual void Add(IEnumerable<object> items)
        {
            foreach (object item in items)
                ContainedObjects.Add(item);

            Bounds();
            base.Distribute(0);
        }

        public virtual void Add(object item)
        {
            ContainedObjects.Add(item);

            Bounds();
            base.Distribute(0);
        }

        public override void ObjectsInFrustum(List<object> objects, Frustum boundingFrustum)
        {
            bool useTree = true;
            if (useTree)
                base.ObjectsInFrustum(objects, boundingFrustum);
            else // brute force to see if our box in frustum works
                AddInFrustum(objects, boundingFrustum, this);
        }

        protected void AddInFrustum(List<object> objects, Frustum boundingFrustum, OctreeLeaf leaf)
        {
            foreach (IOctreeObject item in leaf.containedObjects)
            {
                if (boundingFrustum.Intersects(item.GetOctreeBounds()))
                    objects.Add(item);
            }

            if (leaf.ChildLeaves != null)
            {
                foreach (OctreeLeaf child in leaf.ChildLeaves)
                    AddInFrustum(objects, boundingFrustum, child);
            }
        }

        public override void ObjectsInBoundingBox(List<object> objects, BoundingBox box)
        {
            base.ObjectsInBoundingBox(objects, box);
        }

        public override void ObjectsInBoundingSphere(List<object> objects, SphereShape sphere)
        {
            base.ObjectsInBoundingSphere(objects, sphere);
        }
    }
}
