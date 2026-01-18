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
            VPoint a = new VPoint(
            	10 * Math.Cos(ToRadians(150)),
            	10 * Math.Sin(ToRadians(150))
        	);
        	a.Draw();
        	
        	
            VPoint b = new VPoint(
            	10 * Math.Cos(ToRadians(150)),
            	10 * Math.Sin(ToRadians(-150))
        	);
        	b.Draw();
        	
        	VLine line = new VLine(a, b);
        	line.Draw();
        	VizConsole.Log($"{line.Id}");
        	
        	VPoint c = a + new VPoint(6,0);
        	VPoint d = c + new VPoint(0, -18);
        	
        	c.Draw();
        	d.Draw();
        	
        	double rads = ToRadians(180.0);
        	VizConsole.Log($"{rads}");
        }
        
        
        // 180 dgrees = pi * radians
        public static double ToDegrees(double radians)
        {
        	return (Math.PI * 180) / radians;
        }
        
        public static double ToRadians(double degrees)
        {
        	return (Math.PI * degrees) / 180;
        }
        
    }
}