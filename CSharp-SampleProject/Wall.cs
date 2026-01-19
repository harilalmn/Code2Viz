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
        
        public static Wall FromCurve(ICurve curve, double thickness)
        {
            ICurve c1 = curve.Offset(thickness / 2);
            ICurve c2 = curve.Offset(-thickness / 2);
            
            VPolygon polygon = new VPolygon()
            
            
        }
        
    }
}