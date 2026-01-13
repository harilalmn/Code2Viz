using System;

namespace Code2Viz.Geometry
{
    /// <summary>
    /// Object representing coordinates in 3-dimensional space.
    /// Mimics Autodesk.Revit.DB.XYZ.
    /// </summary>
    public class VXYZ
    {
        // Immutable coordinates
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public VXYZ(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public VXYZ() : this(0, 0, 0) { }

        // Static properties
        public static VXYZ Zero { get; } = new VXYZ(0, 0, 0);
        public static VXYZ BasisX { get; } = new VXYZ(1, 0, 0);
        public static VXYZ BasisY { get; } = new VXYZ(0, 1, 0);
        public static VXYZ BasisZ { get; } = new VXYZ(0, 0, 1);

        // Indexer
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    default: throw new IndexOutOfRangeException("Index must be between 0 and 2");
                }
            }
        }

        // Methods
        public VXYZ Add(VXYZ source) => new VXYZ(X + source.X, Y + source.Y, Z + source.Z);
        public VXYZ Subtract(VXYZ source) => new VXYZ(X - source.X, Y - source.Y, Z - source.Z);
        public VXYZ Multiply(double value) => new VXYZ(X * value, Y * value, Z * value);
        public VXYZ Divide(double value) => new VXYZ(X / value, Y / value, Z / value);
        public VXYZ Negate() => new VXYZ(-X, -Y, -Z);

        /// <summary>
        /// Converts this VXYZ to a VPoint (drops Z component).
        /// </summary>
        public VPoint AsVPoint() => new VPoint(X, Y);

        public double GetLength() => Math.Sqrt(X * X + Y * Y + Z * Z);
        
        public VXYZ Normalize()
        {
            double len = GetLength();
            if (IsZero(len)) return Zero; // Handle zero vector gracefully or throw? Revit throws, but safe is better here.
            return Divide(len);
        }

        public double DistanceTo(VXYZ source)
        {
            return Subtract(source).GetLength();
        }

        public double DotProduct(VXYZ source) => X * source.X + Y * source.Y + Z * source.Z;

        public VXYZ CrossProduct(VXYZ source)
        {
            return new VXYZ(
                Y * source.Z - Z * source.Y,
                Z * source.X - X * source.Z,
                X * source.Y - Y * source.X
            );
        }

        public double TripleProduct(VXYZ a, VXYZ b)
        {
            return DotProduct(a.CrossProduct(b));
        }

        public double AngleTo(VXYZ source)
        {
            double dot = DotProduct(source);
            double lenProd = GetLength() * source.GetLength();
            if (IsZero(lenProd)) return 0;
            
            double cos = dot / lenProd;
            // Clamp to [-1, 1] to handle precision errors
            if (cos > 1.0) cos = 1.0;
            if (cos < -1.0) cos = -1.0;
            
            return Math.Acos(cos);
        }

        // Methods for implementation consistency
        public bool IsZeroLength() => IsZero(GetLength());
        public bool IsUnitLength() => IsZero(GetLength() - 1.0);

        public bool IsAlmostEqualTo(VXYZ source, double tolerance = 1.0e-09)
        {
            return Math.Abs(X - source.X) < tolerance &&
                   Math.Abs(Y - source.Y) < tolerance &&
                   Math.Abs(Z - source.Z) < tolerance;
        }

        public static bool IsWithinLengthLimits(VXYZ point)
        {
            // Revit limits are approx +/- 30,000 ft (~9km). Let's say +/- 1e5 for general use.
            return Math.Abs(point.X) < 1e5 && Math.Abs(point.Y) < 1e5 && Math.Abs(point.Z) < 1e5;
        }

        // Operators
        public static VXYZ operator +(VXYZ a, VXYZ b) => a.Add(b);
        public static VXYZ operator -(VXYZ a, VXYZ b) => a.Subtract(b);
        public static VXYZ operator *(VXYZ a, double b) => a.Multiply(b);
        public static VXYZ operator *(double a, VXYZ b) => b.Multiply(a);
        public static VXYZ operator /(VXYZ a, double b) => a.Divide(b);
        public static VXYZ operator -(VXYZ a) => a.Negate();

        // Equality operators using fuzzy logic
        public static bool operator ==(VXYZ? a, VXYZ? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.IsAlmostEqualTo(b);
        }

        public static bool operator !=(VXYZ? a, VXYZ? b) => !(a == b);

        public override bool Equals(object? obj)
        {
            if (obj is VXYZ v) return this == v;
            return false;
        }

        public override int GetHashCode()
        {
            // Round to a precision consistent with IsAlmostEqualTo (approx 9 decimal places)
            // This is "lossy" but allows the fuzzy equality to work better in collections.
            // Using logic: round to 8 decimals for hashing to group "close" values.
            return HashCode.Combine(Math.Round(X, 8), Math.Round(Y, 8), Math.Round(Z, 8));
        }

        public override string ToString()
        {
            return $"({X:F9}, {Y:F9}, {Z:F9})";
        }

        private static bool IsZero(double val) => Math.Abs(val) < 1.0e-09;
    }
}
