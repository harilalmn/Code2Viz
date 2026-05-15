using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;

namespace PathFinder
{
    public class Viz
    {
        public static void Main()
        {
            // ============================================================
// A* PATHFINDING LAB
// Manual A* with Manhattan heuristic · 4-connected · animated frontier
// ============================================================
ShapeDefaults.Reset();

// ---------- GRID ----------
int W = 20, H = 15;
double cs = 22.0;
var gridOrigin = new VPoint(-W * cs / 2.0, -H * cs / 2.0);
var grid = new VSpatialGrid(gridOrigin, W, H, cs) { Name = "grid" };

// ---------- HEADER ----------
var title = new VText(-220, 195, "A* PATHFINDING LAB", 14) {
    Color = "Gold", FontWeight = VFontWeight.Bold, Name = "title"
};
var sub = new VText(-220, 178, "manual A* · Manhattan heuristic · 4-connected · animated frontier", 9) {
    Color = "Silver", Name = "subtitle"
};

// ---------- OBSTACLES (block cells) ----------
(int c, int r)[] walls = {
    // wall 1: vertical at col 5, gap at row 7
    (5,1),(5,2),(5,3),(5,4),(5,5),(5,6),(5,8),(5,9),(5,10),
    // wall 2: vertical at col 10, gap at row 3
    (10,4),(10,5),(10,6),(10,7),(10,8),(10,9),(10,10),(10,11),(10,12),(10,13),
    // wall 3: vertical at col 15, gap at row 10
    (15,1),(15,2),(15,3),(15,4),(15,5),(15,6),(15,7),(15,8),(15,9),(15,11),(15,12),(15,13),
    // scattered blocks
    (3,12),(7,11),(12,2),(17,11),(2,7),(8,13)
};
foreach (var (c, r) in walls) {
    grid[c, r].Blocked = true;
}

// Visual fill for obstacles
int wIdx = 0;
foreach (var (c, r) in walls) {
    var cell = grid[c, r];
    var rect = new VRectangle(
        cell.Center.X - cs/2 + 1, cell.Center.Y - cs/2 + 1, cs - 2, cs - 2) {
        FillColor = "#3A3F4A", Color = "#1A1E26", LineWeight = 0.5,
        Name = $"obstacle_{wIdx++}"
    };
}

// ---------- START / GOAL ----------
var startCell = grid[1, 1];
var goalCell = grid[18, 13];

var startMarker = new VCircle(startCell.Center.X, startCell.Center.Y, cs/2.4) {
    FillColor = "#5FB85F", Color = "#9FE89F", LineWeight = 1.5, Name = "start"
};
var startLabel = new VText(startCell.Center.X - 4, startCell.Center.Y - 4, "S", 9) {
    Color = "#0A0E1A", FontWeight = VFontWeight.Bold, Name = "start_label"
};

var goalMarker = new VCircle(goalCell.Center.X, goalCell.Center.Y, cs/2.4) {
    FillColor = "#E07050", Color = "#FFB0A0", LineWeight = 1.5, Name = "goal"
};
var goalLabel = new VText(goalCell.Center.X - 4, goalCell.Center.Y - 4, "G", 9) {
    Color = "#0A0E1A", FontWeight = VFontWeight.Bold, Name = "goal_label"
};

// ---------- MANUAL A* ----------
double Heuristic(VCell a, VCell b) =>
    Math.Abs(a.Column - b.Column) + Math.Abs(a.Row - b.Row);

var openList = new List<(VCell cell, double g, double f)>();
var closedSet = new HashSet<(int, int)>();
var cameFrom = new Dictionary<(int, int), VCell>();
var gScore = new Dictionary<(int, int), double>();

openList.Add((startCell, 0, Heuristic(startCell, goalCell)));
gScore[(startCell.Column, startCell.Row)] = 0;

var closedOrder = new List<VCell>();
VCell found = null;
int[,] dirs = { {1,0}, {-1,0}, {0,1}, {0,-1} };

while (openList.Count > 0) {
    openList.Sort((a, b) => a.f.CompareTo(b.f));
    var (curCell, curG, curF) = openList[0];
    openList.RemoveAt(0);

    var key = (curCell.Column, curCell.Row);
    if (closedSet.Contains(key)) continue;
    closedSet.Add(key);
    closedOrder.Add(curCell);

    if (curCell.Column == goalCell.Column && curCell.Row == goalCell.Row) {
        found = curCell;
        break;
    }

    for (int d = 0; d < 4; d++) {
        int nc = curCell.Column + dirs[d, 0];
        int nr = curCell.Row + dirs[d, 1];
        if (nc < 0 || nc >= W || nr < 0 || nr >= H) continue;
        var nb = grid[nc, nr];
        if (nb.Blocked) continue;
        var nkey = (nc, nr);
        if (closedSet.Contains(nkey)) continue;

        double tg = curG + 1;
        if (gScore.TryGetValue(nkey, out double existing) && existing <= tg) continue;

        gScore[nkey] = tg;
        cameFrom[nkey] = curCell;
        openList.Add((nb, tg, tg + Heuristic(nb, goalCell)));
    }
}

// ---------- RECONSTRUCT PATH ----------
var path = new List<VCell>();
if (found != null) {
    var cur = found;
    path.Add(cur);
    while (cameFrom.TryGetValue((cur.Column, cur.Row), out VCell prev)) {
        path.Add(prev);
        cur = prev;
    }
    path.Reverse();
}

// ---------- ANIMATIONS ----------
var animator = new Animator();

// Phase 1: explored cells (pale cyan) — in the order A* expanded them
int eIdx = 0;
foreach (var c in closedOrder) {
    if (c.Column == startCell.Column && c.Row == startCell.Row) continue;
    if (c.Column == goalCell.Column && c.Row == goalCell.Row) continue;
    var rect = new VRectangle(
        c.Center.X - cs/2 + 2, c.Center.Y - cs/2 + 2, cs - 4, cs - 4) {
        FillColor = "#5FB8C8", Color = "Transparent", Opacity = 0,
        Name = $"explored_{eIdx++}"
    };
    animator.AddToAnimations(new FadeInAnimation(rect, 0.018));
}

animator.Pause(0.5);

// Phase 2: path — gold segments connecting cell centers
if (path.Count >= 2) {
    for (int i = 0; i < path.Count - 1; i++) {
        var a = path[i];
        var b = path[i + 1];
        var seg = new VLine(a.Center.X, a.Center.Y, b.Center.X, b.Center.Y) {
            Color = "#D8B85F", LineWeight = 3.2, Name = $"path_{i}"
        };
        animator.AddToAnimations(new DrawAnimation(seg, 0.08));
    }
}

// ---------- STATUS ----------
var status = new VText(-220, -195,
    $"explored: {closedOrder.Count} cells   ·   path length: {path.Count} steps   ·   walls: {walls.Length}",
    10) { Color = "Gold", Name = "status" };

animator.Animate();

VizConsole.Log($"Grid: {W}×{H} = {W*H} cells, {walls.Length} blocked");
VizConsole.Log($"Start: ({startCell.Column}, {startCell.Row})  Goal: ({goalCell.Column}, {goalCell.Row})");
VizConsole.Log($"Manhattan lower bound: {(int)Heuristic(startCell, goalCell)}");
VizConsole.Log($"A* explored: {closedOrder.Count} cells before reaching goal");
VizConsole.Log($"Optimal path: {path.Count} steps ({path.Count - 1} segments)");
        }
    }
}