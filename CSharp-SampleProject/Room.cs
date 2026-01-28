using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
namespace CSharpSample
{
    public class Room : IEquatable<Room>
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Width { get; private set; }
        public int Depth { get; private set; }
        public VPoint Location { get; private set; }
        public VRectangle Boundary
        {
            get
            {
                return getBoundary();
            }
        }
        public VXYZ AxisX { get; private set; }
        public VXYZ AxisY
        {
            get
            {
                return AxisX.CrossProduct(VXYZ.BasisZ);
            }
        }
        public Room(string name, int width, int depth)
        {
            Id = new Guid().ToString();
            Name = name;
            Width = width;
            Depth = depth;
            Location = new VPoint(0, 0);
            AxisX = new VXYZ(1, 0, 0);
        }
        public void Move(VXYZ vector)
        {
            Location += vector;
        }
        public void Rotate(double angleInDegrees, VPoint pivot)
        {
            pivot = pivot ?? Location;
            Location.Rotate(pivot, angleInDegrees);
            AxisX.Rotate(angleInDegrees);
        }
        private VRectangle getBoundary()
        {
            VPoint bottomLeft = Location + AxisX.Negate() * Width / 2 + AxisY.Negate() * Depth / 2;
            VPoint topRight = Location + AxisX * Width / 2 + AxisY * Depth / 2;
            VRectangle rect = new VRectangle(bottomLeft, topRight);
            return rect;
        }
        public void Draw()
        {
            Location.Draw();
            Boundary.Draw();
        }
        public bool Equals(Room? other)
        {
            return Id == other.Id;
        }
        public override string ToString()
            {
                return $"{Name} ({Width} X {Depth})";
        }
    }
}