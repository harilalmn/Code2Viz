using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Animation;
using Code2Viz.Console;

namespace CSharpSample
{
    public class Viz
    {
        public static void Main()
        {
            VPolyline line1 = new VPolyline(
            	new VPoint(0, 0), 
	            new VPoint(20, 0),
	            new VPoint(20, 30),
	            new VPoint(15, 30),
	            new VPoint(15, 35),
	            new VPoint(0, 35),
	            new VPoint(0, 0)
            );
            
            Wall wall1 = new Wall(line1, 0.5);
            wall1.Geometry.Draw();

        }
    }
}