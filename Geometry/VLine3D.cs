using System;

namespace Code2Viz.Geometry
{
    public class VLine3D
    {
        public VXYZ Origin { get; }
        public VXYZ Direction { get; }
        public double Length { get; }
        public VXYZ EndPoint { get; }

        private VLine3D(VXYZ start, VXYZ end)
        {
            Origin = start;
            EndPoint = end;
            VXYZ vector = end.Subtract(start);
            Length = vector.GetLength();
            Direction = Length > 1e-9 ? vector.Normalize() : VXYZ.Zero;
        }

        public static VLine3D CreateBound(VXYZ start, VXYZ end)
        {
            return new VLine3D(start, end);
        }

        public VXYZ GetEndPoint(int index)
        {
            if (index == 0) return Origin;
            if (index == 1) return EndPoint;
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be 0 or 1");
        }
    }
}
