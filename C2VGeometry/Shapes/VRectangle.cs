using System;
using System.Collections.Generic;

namespace C2VGeometry;

public class VRectangle : VPolygon
{
    private VXYZ _corner;
    private double _width;
    private double _height;
    private double _rotationAngle;

    public VXYZ Corner
    {
        get => _corner;
        set
        {
            _corner = value;
            UpdatePoints();
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            _width = value;
            UpdatePoints();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            _height = value;
            UpdatePoints();
        }
    }

    /// <summary>Rotation angle in degrees (counter-clockwise) for the rectangle's intrinsic orientation.</summary>
    public new double RotationAngle
    {
        get => _rotationAngle;
        set
        {
            _rotationAngle = value;
            UpdatePoints();
        }
    }

    public VRectangle(VXYZ corner, double width, double height)
        : base(ComputeCorners(corner, width, height, 0))
    {
        _corner = corner;
        _width = width;
        _height = height;
        _rotationAngle = 0;
        Color = "Magenta";
        FillColor = "Transparent";
    }

    public VRectangle(double x, double y, double width, double height)
        : this(new VXYZ(x, y), width, height)
    {
    }

    /// <summary>
    /// Creates a rectangle from two corner points (bottom-left and top-right).
    /// </summary>
    public VRectangle(VXYZ bottomLeft, VXYZ topRight)
        : this(bottomLeft, topRight.X - bottomLeft.X, topRight.Y - bottomLeft.Y)
    {
    }

    private static VXYZ[] ComputeCorners(VXYZ corner, double width, double height, double rotationAngle)
    {
        var p0 = new VXYZ(corner.X, corner.Y);
        var p1 = new VXYZ(corner.X + width, corner.Y);
        var p2 = new VXYZ(corner.X + width, corner.Y + height);
        var p3 = new VXYZ(corner.X, corner.Y + height);

        if (Math.Abs(rotationAngle) >= 1e-9)
        {
            var center = new VXYZ(corner.X + width / 2, corner.Y + height / 2);

            p0 = GeometryHelper.RotatePoint(p0, center, rotationAngle);
            p1 = GeometryHelper.RotatePoint(p1, center, rotationAngle);
            p2 = GeometryHelper.RotatePoint(p2, center, rotationAngle);
            p3 = GeometryHelper.RotatePoint(p3, center, rotationAngle);
        }

        return new[] { p0, p1, p2, p3 };
    }

    private void UpdatePoints()
    {
        var corners = ComputeCorners(_corner, _width, _height, _rotationAngle);
        Points.Clear();
        Points.AddRange(corners);
        BuildCurvesFromPoints();
    }

    public override List<ControlPoint> GetControlPoints()
    {
        double cx = _corner.X + _width / 2;
        double cy = _corner.Y + _height / 2;
        return new List<ControlPoint>
        {
            new ControlPoint(ControlPointType.Move, cx, cy, "Center"),
            new ControlPoint(ControlPointType.Vertex, _corner.X, _corner.Y, "Corner"),
            new ControlPoint(ControlPointType.Vertex, _corner.X + _width, _corner.Y + _height, "Opposite")
        };
    }

    public override void MoveControlPoint(int index, VXYZ newPosition)
    {
        switch (index)
        {
            case 0: // Move center
                double cx = _corner.X + _width / 2;
                double cy = _corner.Y + _height / 2;
                var delta = new VXYZ(newPosition.X - cx, newPosition.Y - cy, 0);
                Move(delta);
                break;
            case 1: // Bottom-left corner - resize keeping opposite corner fixed
                double oppX = _corner.X + _width;
                double oppY = _corner.Y + _height;
                _corner = new VXYZ(Math.Min(newPosition.X, oppX), Math.Min(newPosition.Y, oppY));
                _width = Math.Abs(oppX - newPosition.X);
                _height = Math.Abs(oppY - newPosition.Y);
                UpdatePoints();
                break;
            case 2: // Top-right corner - resize keeping corner fixed
                _width = Math.Abs(newPosition.X - _corner.X);
                _height = Math.Abs(newPosition.Y - _corner.Y);
                if (newPosition.X < _corner.X)
                    _corner = new VXYZ(newPosition.X, _corner.Y);
                if (newPosition.Y < _corner.Y)
                    _corner = new VXYZ(_corner.X, newPosition.Y);
                UpdatePoints();
                break;
        }
    }

    public override VRectangle Clone()
    {
        var clone = new VRectangle(_corner.Clone(), _width, _height);
        clone._rotationAngle = _rotationAngle;
        clone.UpdatePoints();
        CopyStyleTo(clone);
        return clone;
    }

    public override void Move(VXYZ vector)
    {
        _corner = _corner + vector;
        UpdatePoints();
    }

    public override void Rotate(VXYZ pivot, double angleDegrees)
    {
        _corner = GeometryHelper.RotatePoint(_corner, pivot, angleDegrees);
        _rotationAngle += angleDegrees;
        UpdatePoints();
    }

    public override void Flip(VLine mirrorLine)
    {
        _corner = GeometryHelper.FlipPoint(_corner, mirrorLine);
        UpdatePoints();
    }

    public override void Scale(VXYZ center, double factor)
    {
        _corner = GeometryHelper.ScalePoint(_corner, center, factor);
        _width *= Math.Abs(factor);
        _height *= Math.Abs(factor);
        UpdatePoints();
    }

    public override bool Contains(VXYZ point)
    {
        // For axis-aligned check (no rotation), use simple bounds
        if (Math.Abs(_rotationAngle) < 1e-9)
        {
            return point.X >= _corner.X && point.X <= _corner.X + _width &&
                   point.Y >= _corner.Y && point.Y <= _corner.Y + _height;
        }
        // Otherwise, use polygon containment from base class
        return IsPointInPolygon(point);
    }

    private bool IsPointInPolygon(VXYZ point)
    {
        // Ray casting algorithm
        bool inside = false;
        int j = Points.Count - 1;

        for (int i = 0; i < Points.Count; i++)
        {
            if ((Points[i].Y > point.Y) != (Points[j].Y > point.Y) &&
                point.X < (Points[j].X - Points[i].X) * (point.Y - Points[i].Y) / (Points[j].Y - Points[i].Y) + Points[i].X)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public override Shape? Intersect(Shape other)
    {
        if (other is VRectangle otherRect)
        {
            return GeometryHelper.IntersectRectRect(this, otherRect);
        }
        else if (other is VLine line)
        {
            return GeometryHelper.IntersectLineRect(line, this);
        }
        return base.Intersect(other);
    }

    public override string ToString() => $"VRectangle({_corner}, W:{_width}, H:{_height})";

    public new double GetLength()
    {
        return 2 * (Math.Abs(_width) + Math.Abs(_height));
    }

    public new ICurve Offset(double distance)
    {
        if (Math.Abs(_rotationAngle) < 1e-9)
        {
            double newWidth = _width + 2 * distance;
            double newHeight = _height + 2 * distance;
            return new VRectangle(
                new VXYZ(_corner.X - distance, _corner.Y - distance),
                newWidth, newHeight
            );
        }
        return base.Offset(distance);
    }

    public new List<ICurve> Offset(List<double> distances)
    {
        var list = new List<ICurve>();
        foreach (var d in distances) list.Add(Offset(d));
        return list;
    }
}
