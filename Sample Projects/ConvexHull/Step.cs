using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;

namespace MyVizProject
{
    public class Step
    {
        public int Going { get; private set; }
        public int Rise { get; private set; }
        public VPoint Location { get; private set; }
        public VLine Tread { get; set; }
        public VLine Riser { get; set; }
        public VPoint Start { get; set; }
        public VPoint Mid { get; set; }
        public VPoint End { get; set; }

        public Step(VPoint location, int going, int rise)
        {
            Location = location;
            Going = going;
            Rise = rise;

            Start = (VPoint) location.Clone();
            Mid = Start + new VPoint(0, rise);
            End = Mid + new VPoint(Going, 0);

            Start.Draw();
            Mid.Draw();
            End.Draw();
            VPolyline pl = new VPolyline(new VPoint[]{Start, Mid, End});
            pl.Draw();

        }
    }
}