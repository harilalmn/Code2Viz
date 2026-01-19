using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;

namespace CSharpSample
{
    public class Wall
    {
        public double Thickness  {get; private set;}
        public VPolygon Geometry {get; private set;}
        
        public Wall(ICurve curve, double thickness)
        {
            
            
            ICurve c1 = curve.Offset(thickness / 2);
            ICurve c2 = curve.Offset(-thickness / 2);
            ICurve c3 = new VLine(c1.StartPoint, c2.StartPoint);
            ICurve c4 = new VLine(c1.EndPoint, c2.EndPoint);
            Geometry = new VPolygon(new List<ICurve>(){c1, c2, c3, c4});
            Geometry.FillColor = "Gray";
            Geometry.StrokeColor = "#cccccc";
        }
    }
}