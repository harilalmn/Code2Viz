using System;

namespace Code2Viz.Geometry
{
    public class VPlane
    {
        public VXYZ Origin { get; }
        public VXYZ Normal { get; }
        public VXYZ XVec { get; }
        public VXYZ YVec { get; }

        private VPlane(VXYZ origin, VXYZ xVec, VXYZ yVec)
        {
            Origin = origin;
            XVec = xVec.Normalize();
            YVec = yVec.Normalize();
            Normal = XVec.CrossProduct(YVec).Normalize();
        }

        private VPlane(VXYZ origin, VXYZ normal)
        {
            Origin = origin;
            Normal = normal.Normalize();
             // Construct arbitrary X and Y basis
            if (Math.Abs(Normal.X) > Math.Abs(Normal.Y))
                XVec = new VXYZ(-Normal.Z, 0, Normal.X).Normalize(); // Cross with Y axis (0,1,0) approx
            else
                XVec = new VXYZ(0, Normal.Z, -Normal.Y).Normalize(); // Cross with X axis (1,0,0) approx
            
            // Wait, simpler way to get arbitrary perpendicular:
            // if N is near Z, cross with X.
            // But let's stick to the user's likely usage or standard math.
            
            // Standard method:
            if (Math.Abs(Normal.Z) > 0.999) // Normal is roughly Z
            {
                 XVec = VXYZ.BasisX;
                 if (Math.Abs(Normal.DotProduct(XVec)) > 0.999) XVec = VXYZ.BasisY; // Handle edge case if Normal is X
            }
            else
            {
                XVec = VXYZ.BasisZ.CrossProduct(Normal).Normalize();
            }
            YVec = Normal.CrossProduct(XVec).Normalize();
        }

        public static VPlane CreateByOriginAndBasis(VXYZ origin, VXYZ xVec, VXYZ yVec)
        {
            return new VPlane(origin, xVec, yVec);
        }
        
        public static VPlane CreateByNormalAndOrigin(VXYZ normal, VXYZ origin)
        {
             return new VPlane(origin, normal);
        }

        public static VPlane CreateByThreePoints(VXYZ p1, VXYZ p2, VXYZ p3)
        {
            VXYZ v1 = p2.Subtract(p1);
            VXYZ v2 = p3.Subtract(p1);
            return new VPlane(p1, v1, v2);
        }
    }
}
