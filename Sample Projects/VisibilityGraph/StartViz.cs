using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;

namespace VisibilityGraph
{
    public class Viz
    {
        public static void Main()
        {
// ============================================================
// VISIBILITY MAXIMIZER — boundary hidden, red current-point highlight, 3× fade in
// ============================================================
ShapeDefaults.Reset();

// ---------- HEADER ----------
var title = new VText(-260, 245, "VISIBILITY MAXIMIZER ALONG PATH", 15) {
    Color = "Gold", FontWeight = VFontWeight.Bold, Name = "title"
};
var sub = new VText(-260, 226,
    "world-bounded · 10 samples · 360° polygon · max-area wins", 9) {
    Color = "Silver", Name = "subtitle"
};

// ---------- WORLD BOUNDARY ----------
double wxMin = -290, wxMax = 290, wyMin = -215, wyMax = 200;
var worldRect = new VRectangle(wxMin, wyMin, wxMax - wxMin, wyMax - wyMin) {
    Color = "#404060", LineType = LineType.Dashed, LineWeight = 1, LineTypeScale = 0.7,
    Name = "world_rect"
};
var wTL = new VPoint(wxMin, wyMax) { Name = "w_tl" };
var wTR = new VPoint(wxMax, wyMax) { Name = "w_tr" };
var wBR = new VPoint(wxMax, wyMin) { Name = "w_br" };
var wBL = new VPoint(wxMin, wyMin) { Name = "w_bl" };

// ---------- OBSTACLES ----------
var triPts = new List<VPoint>();
triPts.Add(new VPoint(-180, 70) { Name = "tri_0" });
triPts.Add(new VPoint(-75, 145) { Name = "tri_1" });
triPts.Add(new VPoint(-40, 35)  { Name = "tri_2" });

var pentPts = new List<VPoint>();
pentPts.Add(new VPoint(70, 130)  { Name = "pent_0" });
pentPts.Add(new VPoint(195, 90)  { Name = "pent_1" });
pentPts.Add(new VPoint(205, -25) { Name = "pent_2" });
pentPts.Add(new VPoint(115, -65) { Name = "pent_3" });
pentPts.Add(new VPoint(40, 25)   { Name = "pent_4" });

var quadPts = new List<VPoint>();
quadPts.Add(new VPoint(-90, -120) { Name = "quad_0" });
quadPts.Add(new VPoint(45, -90)   { Name = "quad_1" });
quadPts.Add(new VPoint(80, -180)  { Name = "quad_2" });
quadPts.Add(new VPoint(-55, -195) { Name = "quad_3" });

var tri  = new VPolygon(triPts)  { Color = "#C9A781", FillColor = "#3A2D22", LineWeight = 1.5, Name = "tri" };
var pent = new VPolygon(pentPts) { Color = "#7FB89C", FillColor = "#1F3329", LineWeight = 1.5, Name = "pent" };
var quad = new VPolygon(quadPts) { Color = "#9F82C2", FillColor = "#2D213A", LineWeight = 1.5, Name = "quad" };

tri.Draw();
pent.Draw();
quad.Draw();

var obsPts = new List<List<VPoint>>();
obsPts.Add(triPts); obsPts.Add(pentPts); obsPts.Add(quadPts);

var obsEdges = new List<(VPoint a, VPoint b)>();
foreach (var op in obsPts)
    for (int i = 0; i < op.Count; i++)
        obsEdges.Add((op[i], op[(i + 1) % op.Count]));
obsEdges.Add((wTL, wTR)); obsEdges.Add((wTR, wBR));
obsEdges.Add((wBR, wBL)); obsEdges.Add((wBL, wTL));

var visGraphEdges = new List<(VPoint a, VPoint b)>();
foreach (var op in obsPts)
    for (int i = 0; i < op.Count; i++)
        visGraphEdges.Add((op[i], op[(i + 1) % op.Count]));

// ---------- START / GOAL ----------
var start = new VPoint(-265, -25) { Name = "start_pt" };
var goal  = new VPoint(265, -10)  { Name = "goal_pt" };
var startDot = new VCircle(start.X, start.Y, 7) { Color = "Lime", FillColor = "Lime", LineWeight = 2, Name = "start_dot" };
var goalDot  = new VCircle(goal.X,  goal.Y,  7) { Color = "Gold", FillColor = "Gold", LineWeight = 2, Name = "goal_dot" };
var sLab = new VText(start.X - 22, start.Y - 14, "START", 9) { Color = "Lime", Name = "s_lab" };
var gLab = new VText(goal.X  - 16, goal.Y  - 14, "GOAL",  9) { Color = "Gold", Name = "g_lab" };

// ---------- HELPERS ----------
VPoint SegInt(VPoint a, VPoint b, VPoint c, VPoint d) {
    double dx1 = b.X - a.X, dy1 = b.Y - a.Y;
    double dx2 = d.X - c.X, dy2 = d.Y - c.Y;
    double denom = dx1 * dy2 - dy1 * dx2;
    if (Math.Abs(denom) < 1e-9) return null;
    double t = ((c.X - a.X) * dy2 - (c.Y - a.Y) * dx2) / denom;
    double u = ((c.X - a.X) * dy1 - (c.Y - a.Y) * dx1) / denom;
    if (t > 0.0005 && t < 0.9995 && u > 0.0005 && u < 0.9995)
        return new VPoint(a.X + t * dx1, a.Y + t * dy1);
    return null;
}

VPoint RayHit(VPoint p, VPoint rayEnd, VPoint a, VPoint b) {
    double dx1 = rayEnd.X - p.X, dy1 = rayEnd.Y - p.Y;
    double dx2 = b.X - a.X, dy2 = b.Y - a.Y;
    double denom = dx1 * dy2 - dy1 * dx2;
    if (Math.Abs(denom) < 1e-9) return null;
    double t = ((a.X - p.X) * dy2 - (a.Y - p.Y) * dx2) / denom;
    double u = ((a.X - p.X) * dy1 - (a.Y - p.Y) * dx1) / denom;
    if (t > 0.001 && t <= 1.0 && u >= -0.0001 && u <= 1.0001)
        return new VPoint(p.X + t * dx1, p.Y + t * dy1);
    return null;
}

// ---------- VISIBILITY GRAPH + DIJKSTRA (silent) ----------
var allVerts = new List<VPoint>();
var vertObs = new List<int>(); var vertLocal = new List<int>();
for (int o = 0; o < obsPts.Count; o++)
    for (int v = 0; v < obsPts[o].Count; v++) {
        allVerts.Add(obsPts[o][v]); vertObs.Add(o); vertLocal.Add(v);
    }
int sIdx = allVerts.Count; allVerts.Add(start); vertObs.Add(-1); vertLocal.Add(0);
int gIdx = allVerts.Count; allVerts.Add(goal);  vertObs.Add(-2); vertLocal.Add(0);
int N = allVerts.Count;

var visEdges = new List<(int i, int j, double w)>();
for (int i = 0; i < N; i++) for (int j = i + 1; j < N; j++) {
    var pi = allVerts[i]; var pj = allVerts[j];
    bool blocked = false;
    if (vertObs[i] >= 0 && vertObs[i] == vertObs[j]) {
        int polyN = obsPts[vertObs[i]].Count;
        int diff = Math.Abs(vertLocal[i] - vertLocal[j]);
        if (!(diff == 1 || diff == polyN - 1)) blocked = true;
    }
    if (!blocked) foreach (var (a, b) in visGraphEdges) {
        if (a == pi || a == pj || b == pi || b == pj) continue;
        if (SegInt(pi, pj, a, b) != null) { blocked = true; break; }
    }
    if (!blocked) {
        double dd = Math.Sqrt((pi.X - pj.X) * (pi.X - pj.X) + (pi.Y - pj.Y) * (pi.Y - pj.Y));
        visEdges.Add((i, j, dd));
    }
}

var graph = new Dictionary<int, List<(int, double)>>();
for (int i = 0; i < N; i++) graph[i] = new List<(int, double)>();
foreach (var (i, j, w) in visEdges) { graph[i].Add((j, w)); graph[j].Add((i, w)); }
var distArr = new double[N]; var prevArr = new int[N];
for (int i = 0; i < N; i++) { distArr[i] = double.MaxValue; prevArr[i] = -1; }
distArr[sIdx] = 0;
var seen = new bool[N];
for (int step = 0; step < N; step++) {
    int u = -1; double best = double.MaxValue;
    for (int k = 0; k < N; k++) if (!seen[k] && distArr[k] < best) { u = k; best = distArr[k]; }
    if (u == -1 || u == gIdx) break;
    seen[u] = true;
    foreach (var (v, w) in graph[u]) {
        double nd = distArr[u] + w;
        if (nd < distArr[v]) { distArr[v] = nd; prevArr[v] = u; }
    }
}
var pathIdx = new List<int>();
int cur = gIdx;
while (cur != -1) { pathIdx.Add(cur); cur = prevArr[cur]; }
pathIdx.Reverse();
var pathPts = pathIdx.Select(k => allVerts[k]).ToList();

var shortestPath = new VPolyline(pathPts.ToArray()) {
    Color = "#FFC838", LineWeight = 4, Name = "shortest_path"
    
};
    shortestPath.Draw();

// ---------- 10 SAMPLES ALONG PATH ----------
double totalLen = 0;
var segLens = new List<double>();
for (int i = 0; i < pathPts.Count - 1; i++) {
    var a = pathPts[i]; var b = pathPts[i + 1];
    double L = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
    segLens.Add(L); totalLen += L;
}
int numSamples = 10;
var samples = new List<VPoint>();
for (int i = 0; i < numSamples; i++) {
    double t = (double)i / (numSamples - 1);
    double target = t * totalLen;
    double accum = 0;
    for (int s = 0; s < segLens.Count; s++) {
        if (accum + segLens[s] >= target - 1e-9) {
            double localT = (target - accum) / segLens[s];
            var a = pathPts[s]; var b = pathPts[s + 1];
            samples.Add(new VPoint(a.X + localT * (b.X - a.X), a.Y + localT * (b.Y - a.Y)) {
                Name = $"sample_pt_{i}"
            });
            break;
        }
        accum += segLens[s];
    }
}

// Persistent cyan dots (visible throughout)
for (int i = 0; i < samples.Count; i++) {
    var dot = new VCircle(samples[i].X, samples[i].Y, 3.5) {
        Color = "Cyan", FillColor = "#0A2F3F", LineWeight = 1.5, Name = $"sample_dot_{i}"
    };
}

// *** RED HIGHLIGHT DOTS — one per sample, hidden by DrawAnimation until its turn ***
var redDots = new List<VCircle>();
for (int i = 0; i < samples.Count; i++) {
    var rd = new VCircle(samples[i].X, samples[i].Y, 5) {
        Color = "Red", FillColor = "Red", LineWeight = 2,
        Name = $"red_dot_{i}"
    };
    redDots.Add(rd);
}

// ---------- VISIBILITY POLYGONS (boundary HIDDEN) ----------
double maxDist = 1000;
int numRays = 90;

var visPolys = new List<VPolygon>();
var areas = new List<double>();
int maxIdx = 0; double maxArea = 0;

for (int s = 0; s < samples.Count; s++) {
    var p = samples[s];
    var verts = new List<VPoint>();
    for (int r = 0; r < numRays; r++) {
        double ang = r * 2 * Math.PI / numRays;
        var rayEnd = new VPoint(p.X + maxDist * Math.Cos(ang), p.Y + maxDist * Math.Sin(ang));
        if (rayEnd.X < wxMin || rayEnd.X > wxMax || rayEnd.Y < wyMin || rayEnd.Y > wyMax)
            rayEnd.Hide();

        VPoint closest = rayEnd;
        double minDsq = maxDist * maxDist;
        foreach (var (a, b) in obsEdges) {
            var hit = RayHit(p, rayEnd, a, b);
            if (hit != null) {
                double dsq = (hit.X - p.X) * (hit.X - p.X) + (hit.Y - p.Y) * (hit.Y - p.Y);
                if (dsq < minDsq) { minDsq = dsq; closest = hit; }
            }
        }
        verts.Add(closest);
    }
    var poly = new VPolygon(verts) {
        Color = "Transparent", FillColor = "#30FFCC00", LineWeight = 0,
        Name = $"vis_poly_{s}"
    };
    visPolys.Add(poly);
    double area = Math.Abs(poly.Area);
    areas.Add(area);
    if (area > maxArea) { maxArea = area; maxIdx = s; }
}

// ---------- MAX VISIBILITY POLYGON (keep outline visible for emphasis) ----------
var maxSample = samples[maxIdx];
var maxVerts = new List<VPoint>();
for (int r = 0; r < numRays; r++) {
    double ang = r * 2 * Math.PI / numRays;
    var rayEnd = new VPoint(maxSample.X + maxDist * Math.Cos(ang), maxSample.Y + maxDist * Math.Sin(ang));
    if (rayEnd.X < wxMin || rayEnd.X > wxMax || rayEnd.Y < wyMin || rayEnd.Y > wyMax)
        rayEnd.Hide();

    VPoint closest = rayEnd;
    double minDsq = maxDist * maxDist;
    foreach (var (a, b) in obsEdges) {
        var hit = RayHit(maxSample, rayEnd, a, b);
        if (hit != null) {
            double dsq = (hit.X - maxSample.X) * (hit.X - maxSample.X) + (hit.Y - maxSample.Y) * (hit.Y - maxSample.Y);
            if (dsq < minDsq) { minDsq = dsq; closest = hit; }
        }
    }
    maxVerts.Add(closest);
}
var maxPoly = new VPolygon(maxVerts) {
    Color = "#FFE058", FillColor = "#60FFCC00", LineWeight = 2.5,
    Name = "max_vis_poly"
};
var maxDot = new VCircle(maxSample.X, maxSample.Y, 9) {
    Color = "Gold", FillColor = "Gold", LineWeight = 2.5,
    Name = "max_marker"
};
var maxLabel = new VText(maxSample.X + 14, maxSample.Y + 10, "MAX VISIBILITY", 11) {
    Color = "Gold", FontWeight = VFontWeight.Bold, Name = "max_label"
};

var status = new VText(-260, -240,
    $"world: 580×415 · path: {pathPts.Count} hops · samples: {numSamples} · winner: #{maxIdx} (area={maxArea:F0})",
    10) { Color = "Gold", Name = "status" };

// ---------- ANIMATION ----------
// Per sample: red dot in → polygon in (1.5s = 3× original) → polygon out → red dot out
var animator = new Animator();
for (int s = 0; s < visPolys.Count; s++) {
    animator.AddToAnimations(new DrawAnimation(redDots[s], 0.2));
    animator.AddToAnimations(new DrawAnimation(visPolys[s], 0.5));
    animator.Pause(0.25);
    animator.AddToAnimations(new FadeOutAnimation(visPolys[s], 0.5));
    animator.AddToAnimations(new FadeOutAnimation(redDots[s], 0.3));
}
animator.AddToAnimations(new DrawAnimation(maxPoly, 1.5));
animator.AddToAnimations(new DrawAnimation(maxDot, 0.5));
animator.AddToAnimations(new DrawAnimation(maxLabel, 0.5));
animator.Animate();

VizConsole.Log($"World: ({wxMin},{wyMin}) to ({wxMax},{wyMax})  ·  path: {pathPts.Count} hops, {totalLen:F1}u");
VizConsole.Log($"=> winner: #{maxIdx} at ({maxSample.X:F0},{maxSample.Y:F0}), area={maxArea:F0}");
VizConsole.Log($"Total animation: ~{visPolys.Count * 2.5 + 2.5:F1}s");
        }
    }
}