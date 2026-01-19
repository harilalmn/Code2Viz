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
            // Wall wall = new Wall(line1);
            VPoint point = new VPoint(0,0);
            VXYZ v = new VXYZ(15,0,0);
            
            double n = 1;
            
            Animation a1 = new MoveAnimation(point, v, 0, n);
            a1.EasingFunction = EasingFunctions.EaseInQuad;
            
            Animation a2 = new FadeOutAnimation(point, n, n/2) ;
            a2.EasingFunction = EasingFunctions.EaseInQuad;
            
            
            Timeline t = new Timeline();
            t.Repeat = true;
            t.Duration = 0.5;
            
            t.AddAnimation(a1);
            t.AddAnimation(a2);
            
            t.Play();
        }
    }
}
