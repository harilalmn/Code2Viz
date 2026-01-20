using System;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Animation;
using Code2Viz.Console;
using System.Linq;

namespace CSharpSample
{
    public class Viz
    {
        public static void Main()
        {
            VGrid grid = new VGrid(new VPoint(0, 0), 50, 50, 100, 100);
            grid.StrokeColor = "Orange";
            // grid.Draw();


            Room room1 = new Room("room1", 2000, 3000);
            Room room2 = new Room("room2", 2000, 3000);

            Random random = new Random();
            VPoint placementPoint = grid[random.Next(grid.Count)];
            room1.Move((placementPoint - room1.Location).AsVXYZ());

            foreach (var p in grid.Points)
            {
                if (room1.IsPointInside(p)) continue;
                room2.Move((p - room2.Location).AsVXYZ());
                if (room1.Overlaps(room2) == false) break;

                for (int i = 0; i < 4; i += 90)
                {
                    room2.Rotate(90, p);
                    if (room1.Overlaps(room2) == false) break;
                }
                
                if (room1.Overlaps(room2) == false) break;
            }

            room1.Move((placementPoint - room1.Location).AsVXYZ());
            room1.Draw();
            room2.Draw();
        }
    }
}