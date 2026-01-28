using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
namespace CSharpSample
{
    public class Viz
    {
        public static void Main()
        {
            IEnumerable<string> lines = File.ReadLines(@".\roomsizes.txt");
            int n = 0;
            List<Room> rooms = new List<Room>();
            foreach (var line in lines)
            {
                int width = Convert.ToInt32(line.Split("X")[0].Trim());
                int depth = Convert.ToInt32(line.Split("X")[1].Trim());
                Room room = new Room($"Room - {n}", width, depth);
                rooms.Add(room);
                n++;
            }
            
            
            
            Room[] roomsArray = rooms.ToArray();
            Random.Shared.Shuffle<Room>(roomsArray);
            List<Room> SelectedRooms = roomsArray.Take(5).ToList();
            
            
            foreach (var item in SelectedRooms)
            {
                VizConsole.Log($"{item}");
                item
            }
        }
    }
}