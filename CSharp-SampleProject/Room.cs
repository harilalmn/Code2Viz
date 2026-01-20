using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using System.Linq;

namespace CSharpSample
{
    public class Room
    {
        public string Name { get; private set; }
        public double Width { get; private set; }
        public double Depth { get; private set; }
        public VPolygon Geometry {get; private set; }
        public VPoint Location {get; private set; }

        public Room(string name, int width, int depth)
        {
            Name = name;
            Width = width;
            Depth = depth;
            Location = new VPoint(0, 0);
            Geometry = new VPolygon(
            Location + new VPoint(- Width / 2, - Depth / 2),
            Location + new VPoint(Width / 2, - Depth / 2),
            Location + new VPoint(Width / 2, Depth / 2),
            Location + new VPoint(- Width / 2, Depth / 2)
        );

        Geometry.StrokeColor = VColor.GetRandomPastelColor();
        Geometry.FillColor = VColor.GetRandomPastelColor();
    }

    public void Move(VXYZ vector)
    {
        Location += vector;
        Geometry.Move(vector);
    }

    public void Rotate(double angleInDegrees, VPoint pivot = null)
    {
        if (pivot == null) pivot = Location;
        Geometry.Rotate(pivot, angleInDegrees);
    }

    public void Draw()
    {
        Geometry.Draw();
    }

    public bool Overlaps(Room otherRoom)
    {
        List<VPolygon> polygons = new List<VPolygon>(){Geometry, otherRoom.Geometry};
        var totalArea = polygons.Select(p => p.Area).Sum();
        VizConsole.Log($"{totalArea}");

        VPolygon union = null;
        try
        {
            union = BooleanOps.Union(polygons);

        }
        catch (Exception ex)
        {
            VizConsole.Log($"{ex.Message}");
        }
        VizConsole.Log($"{union.Area}");

        return union == null || totalArea > union.Area ? true : false;
    }

    public bool IsPointInside(VPoint point)
    {
        return Geometry.Contains(point);
    }
}
}