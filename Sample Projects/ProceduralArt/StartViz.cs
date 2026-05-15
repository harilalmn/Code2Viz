using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;

namespace ProceduralArt
{
    public class Viz
    {
        public static void Main()
        {
            // ============================================================
// PROCEDURAL ART CAROUSEL
// L-system tree · Penrose tiling · Hilbert curve · Truchet tiles
// ============================================================
ShapeDefaults.Reset();

// ---------- HEADERS ----------
var mTitle = new VText(-185, 233, "PROCEDURAL ART · RECURSION + TRANSFORMS", 13) {
    Color = "Gold", FontWeight = VFontWeight.Bold, Name = "master_title"
};

var tlTitle = new VText(-250, 210, "L-SYSTEM TREE", 10) {
    Color = "#7AAA5A", FontWeight = VFontWeight.Bold, Name = "tl_title"
};
var trTitle = new VText(50, 210, "PENROSE TILING", 10) {
    Color = "#E07050", FontWeight = VFontWeight.Bold, Name = "tr_title"
};
var blTitle = new VText(-250, -20, "HILBERT CURVE", 10) {
    Color = "#A86FC8", FontWeight = VFontWeight.Bold, Name = "bl_title"
};
var brTitle = new VText(75, -20, "TRUCHET TILES", 10) {
    Color = "#D8B85F", FontWeight = VFontWeight.Bold, Name = "br_title"
};

var hSep = new VLine(-310, -5, 310, -5) { Color = "#252830", LineWeight = 0.5, Name = "h_sep" };
var vSep = new VLine(0, 220, 0, -240) { Color = "#252830", LineWeight = 0.5, Name = "v_sep" };

var animator = new Animator();

// =================================================================
// QUADRANT TL: L-SYSTEM FRACTAL TREE
// Depth-gradient color (brown trunk → green tips), thickness tapers
// =================================================================
var treeData = ProcArt.Tree(-170, 5, 90, 28, 7, 24, 0.72);
int treeIdx = 0;
foreach (var (x1, y1, x2, y2, depth) in treeData) {
    double t = (double)depth / 7.0;
    int r = (int)(0x5A + t * (0x7A - 0x5A));
    int g = (int)(0x3A + t * (0xAA - 0x3A));
    int b = (int)(0x1F + t * (0x5A - 0x1F));
    var line = new VLine(x1, y1, x2, y2) {
        Color = $"#{r:X2}{g:X2}{b:X2}",
        LineWeight = Math.Max(0.6, 2.5 - depth * 0.32),
        Name = $"tree_{treeIdx++}"
    };
    animator.AddToAnimations(new DrawAnimation(line, 0.022));
}
animator.Pause(0.6);

// =================================================================
// QUADRANT TR: PENROSE P3 TILING
// Radial fade-in from sun center, golden-ratio deflation depth 3
// =================================================================
var pen = ProcArt.Penrose(150, 100, 92, 3);
pen.Sort((a, b) => {
    double aDx = (a.ax + a.bx + a.cx) / 3.0 - 150.0;
    double aDy = (a.ay + a.by + a.cy) / 3.0 - 100.0;
    double bDx = (b.ax + b.bx + b.cx) / 3.0 - 150.0;
    double bDy = (b.ay + b.by + b.cy) / 3.0 - 100.0;
    return (aDx*aDx + aDy*aDy).CompareTo(bDx*bDx + bDy*bDy);
});

int penIdx = 0;
foreach (var (col, ax, ay, bx, by, cx, cy) in pen) {
    var pts = new List<VPoint>();
    var pa = new VPoint(ax, ay); pa.Hide();
    var pb = new VPoint(bx, by); pb.Hide();
    var pc = new VPoint(cx, cy); pc.Hide();
    pts.Add(pa); pts.Add(pb); pts.Add(pc);
    var poly = new VPolygon(pts) {
        Color = "#0A0E1A",
        FillColor = col == 0 ? "#E07050" : "#5FB8C8",
        LineWeight = 0.5,
        Opacity = 0,
        Name = $"pen_{penIdx++}"
    };
    animator.AddToAnimations(new FadeInAnimation(poly, 0.025));
}
animator.Pause(0.5);

// =================================================================
// QUADRANT BL: HILBERT SPACE-FILLING CURVE
// Order 4 → 255 segments, violet → gold gradient along the curve
// (individual VLines, NOT VPolyline — VPolyline creates O(N²) phantoms)
// =================================================================
var hilPts = ProcArt.Hilbert(-230, -215, 160, 4);
int hilCount = hilPts.Count - 1;
for (int i = 0; i < hilCount; i++) {
    var p1 = hilPts[i];
    var p2 = hilPts[i + 1];
    double t = (double)i / hilCount;
    int r = (int)(0x6F + t * 0x69);   // 0x6F → 0xD8
    int g = (int)(0x4F + t * 0x69);   // 0x4F → 0xB8
    int b = (int)(0xB8 - t * 0x59);   // 0xB8 → 0x5F
    var seg = new VLine(p1.x, p1.y, p2.x, p2.y) {
        Color = $"#{r:X2}{g:X2}{b:X2}",
        LineWeight = 1.4,
        Name = $"hil_{i}"
    };
    animator.AddToAnimations(new DrawAnimation(seg, 0.012));
}
animator.Pause(0.5);

// =================================================================
// QUADRANT BR: TRUCHET TILES
// 7×7 grid, shuffled draw order, gold arcs on dark canvas
// =================================================================
var tru = ProcArt.Truchet(150, -135, 175, 7, 42);
var rng = new Random(123);
tru = tru.OrderBy(t => rng.Next()).ToList();

int truIdx = 0;
foreach (var (a1, a2) in tru) {
    var arc1 = new VArc(a1.cx, a1.cy, a1.r, a1.s, a1.e) {
        Color = "#D8B85F", LineWeight = 1.6, Name = $"tru_a_{truIdx}"
    };
    var arc2 = new VArc(a2.cx, a2.cy, a2.r, a2.s, a2.e) {
        Color = "#D8B85F", LineWeight = 1.6, Name = $"tru_b_{truIdx}"
    };
    animator.AddToAnimations(new DrawAnimation(arc1, 0.028));
    animator.AddToAnimations(new DrawAnimation(arc2, 0.028));
    truIdx++;
}

animator.Animate();

VizConsole.Log($"Tree lines:        {treeData.Count}");
VizConsole.Log($"Penrose triangles: {pen.Count}");
VizConsole.Log($"Hilbert segments:  {hilCount}");
VizConsole.Log($"Truchet arcs:      {tru.Count * 2}");
        }
    }
}