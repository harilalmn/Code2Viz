using System.Collections.Generic;
using Code2Viz.Geometry;

namespace Code2Viz.Geometry
{
    public abstract class VBox
    {
        private static long counter;

        public long Id { get; }

        // Size
        public double Width { get; private set; }
        public double Depth { get; private set; }

        // Location
        public VXYZ Origin { get; protected set; }

        // Directions
        public VXYZ FrontDirection { get; protected set; }
        public VXYZ BackDirection => FrontDirection.Negate();
        public VXYZ RightDirection { get; protected set; }
        public VXYZ LeftDirection => RightDirection.Negate();

        // Mirror Planes
        public VPlane PlaneFrontBack => VPlane.CreateByOriginAndBasis(Origin, FrontDirection, VXYZ.BasisZ);
        public VPlane PlaneLeftRight => VPlane.CreateByOriginAndBasis(Origin, LeftDirection, VXYZ.BasisZ);

        // Corners
        public VXYZ CornerFrontLeft => Origin + LeftDirection * Width / 2 + FrontDirection * Depth / 2;
        public VXYZ CornerFrontRight => Origin + RightDirection * Width / 2 + FrontDirection * Depth / 2;
        public VXYZ CornerBackLeft => Origin + LeftDirection * Width / 2 + BackDirection * Depth / 2;
        public VXYZ CornerBackRight => Origin + RightDirection * Width / 2 + BackDirection * Depth / 2;

        // Geometry
        public List<VLine3D> Boundary
        {
            get
            {
                VLine3D line1 = VLine3D.CreateBound(CornerFrontLeft, CornerBackLeft);
                VLine3D line2 = VLine3D.CreateBound(CornerBackLeft, CornerBackRight);
                VLine3D line3 = VLine3D.CreateBound(CornerBackRight, CornerFrontRight);
                VLine3D line4 = VLine3D.CreateBound(CornerFrontRight, CornerFrontLeft);
                return new List<VLine3D>() { line1, line2, line3, line4 };
            }
        }

        protected VBox(double width, double depth)
        {
            Id = ++counter;
            Width = width;
            Depth = depth;
            Origin = VXYZ.Zero;
            FrontDirection = VXYZ.BasisY;
            RightDirection = VXYZ.BasisX;
        }

        /// <summary>
        /// Resets orientation to default (Y+ front, X+ right) while preserving position.
        /// </summary>
        public void ResetOrientation()
        {
            FrontDirection = VXYZ.BasisY;
            RightDirection = VXYZ.BasisX;
        }

        /// <summary>
        /// Resets both position and orientation to default values.
        /// </summary>
        public void ResetAll()
        {
            Origin = VXYZ.Zero;
            FrontDirection = VXYZ.BasisY;
            RightDirection = VXYZ.BasisX;
        }

        public void CopyOrientationFrom(VBox source)
        {
            FrontDirection = source.FrontDirection;
            RightDirection = source.RightDirection;
        }

        public virtual void Move(VXYZ translationVector)
        {
            Origin += translationVector;
        }

        public virtual void Rotate(double angleInDegrees)
        {
            VTransform rotation = VTransform.CreateRotation(VXYZ.BasisZ, angleInDegrees.ToRadians());
            FrontDirection = rotation.OfVector(FrontDirection);
            RightDirection = rotation.OfVector(RightDirection);
        }

        public virtual void RotateAround(VXYZ pivot, double angleInDegrees)
        {
            VTransform rotation = VTransform.CreateRotation(VXYZ.BasisZ, angleInDegrees.ToRadians());

            VXYZ relativePosition = Origin - pivot;
            VXYZ rotatedRelativePosition = rotation.OfVector(relativePosition);
            Origin = pivot + rotatedRelativePosition;

            FrontDirection = rotation.OfVector(FrontDirection);
            RightDirection = rotation.OfVector(RightDirection);
        }

        public virtual void FlipFacing()
        {
            VTransform reflection = VTransform.CreateReflection(PlaneLeftRight);
            FrontDirection = reflection.OfVector(FrontDirection);
        }

        public virtual void FlipSides()
        {
            VTransform reflection = VTransform.CreateReflection(PlaneFrontBack);
            RightDirection = reflection.OfVector(RightDirection);
        }

        public virtual void FlipAlong(VLine3D mirrorLine)
        {
            VPlane plane = VPlane.CreateByThreePoints(
                mirrorLine.GetEndPoint(0),
                mirrorLine.GetEndPoint(1),
                mirrorLine.GetEndPoint(1) + VXYZ.BasisZ
            );
            VTransform reflection = VTransform.CreateReflection(plane);
            Origin = reflection.OfPoint(Origin);
            FrontDirection = reflection.OfVector(FrontDirection);
            RightDirection = reflection.OfVector(RightDirection);
        }
    }
    
    public static class DoubleExtensions
    {
         public static double ToRadians(this double degrees)
         {
             return degrees * (System.Math.PI / 180.0);
         }
    }
}
