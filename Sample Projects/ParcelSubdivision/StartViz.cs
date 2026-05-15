using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;

namespace ParcelSubdivision
{
    public class Viz
    {
        public static void Main()
        {
// ============================================================
// PARCEL SUBDIVISION
// BooleanOps.Intersect / .Difference + bisection search → N equal pieces
// ============================================================
ShapeDefaults.Reset();

// ---------- HEADER ----------
var title = new VText(-220, 245, "PARCEL SUBDIVISION", 16) {
    Color = "Gold", FontWeight = VFontWeight.Bold, Name = "title"
};
var sub = new VText(-220, 226, "BooleanOps + bisection search → 5 equal-area pieces", 10) {
    Color = "Silver", Name = "subtitle"
};

// ---------- PARCEL: convex hexagon ----------
var parcelPts = new List<VPoint>();
parcelPts.Add(new VPoint(-200, 100)  { Name = "pp_0" });
parcelPts.Add(new VPoint(150,  130)  { Name = "pp_1" });
parcelPts.Add(new VPoint(220,  30)   { Name = "pp_2" });
parcelPts.Add(new VPoint(180,  -120) { Name = "pp_3" });
parcelPts.Add(new VPoint(-50,  -150) { Name = "pp_4" });
parcelPts.Add(new VPoint(-220, -50)  { Name = "pp_5" });

var parcel = new VPolygon(parcelPts) {
    Color = "Cyan", FillColor = "#0E1A2C", LineWeight = 2.5, Name = "parcel"
};

double totalArea  = Math.Abs(parcel.Area);
int N             = 5;
double targetArea = totalArea / N;

var pieceColors = new[] { "#FF8855", "#5588FF", "#FFCC55", "#55FFCC", "#CC55FF" };
var pieceFills  = new[] { "#80FF8855", "#805588FF", "#80FFCC55", "#8055FFCC", "#80CC55FF" };

double xMin = parcelPts.Min(p => p.X);
double xMax = parcelPts.Max(p => p.X);
double yMin = parcelPts.Min(p => p.Y);
double yMax = parcelPts.Max(p => p.Y);

// helper: build a left-half-plane rectangle (vertices hidden, returned polygon also hidden)
VPolygon MakeLeftRect(double cutX) {
    var pts = new List<VPoint>();
    var v0 = new VPoint(xMin - 80, yMin - 80); v0.Hide();
    var v1 = new VPoint(cutX,      yMin - 80); v1.Hide();
    var v2 = new VPoint(cutX,      yMax + 80); v2.Hide();
    var v3 = new VPoint(xMin - 80, yMax + 80); v3.Hide();
    pts.Add(v0); pts.Add(v1); pts.Add(v2); pts.Add(v3);
    var rect = new VPolygon(pts);
    rect.Hide();
    return rect;
}

var animator = new Animator();
animator.AddToAnimations(new DrawAnimation(parcel, 1.8));

VPolygon remaining = parcel;
int iters = 6;

for (int cutIdx = 0; cutIdx < N - 1; cutIdx++) {
    double low = xMin, high = xMax;
    VLine prevCand = null;

    // ----- bisection iterations -----
    for (int i = 0; i < iters; i++) {
        double mid = (low + high) / 2;

        var candLine = new VLine(mid, yMin - 15, mid, yMax + 15) {
            Color = "Yellow", LineType = LineType.Dashed, LineWeight = 1.5,
            Name = $"cand_{cutIdx}_{i}"
        };
        animator.AddToAnimations(new DrawAnimation(candLine, 0.15));
        if (prevCand != null)
            animator.AddToAnimations(new FadeOutAnimation(prevCand, 0.15));

        var leftRect = MakeLeftRect(mid);
        var leftPieces = BooleanOps.Intersect(remaining, leftRect);
        foreach (var p in leftPieces) p.Hide();
        double cutArea = leftPieces.Sum(p => Math.Abs(p.Area));

        if (cutArea < targetArea) low = mid;
        else                       high = mid;

        prevCand = candLine;
    }
    animator.AddToAnimations(new FadeOutAnimation(prevCand, 0.2));

    // ----- final converged cut -----
    double finalCut = (low + high) / 2;
    var finalLeftRect = MakeLeftRect(finalCut);

    var leftPiecesFinal  = BooleanOps.Intersect(remaining, finalLeftRect);
    var rightPiecesFinal = BooleanOps.Difference(remaining, finalLeftRect);

    var piece = leftPiecesFinal[0];
    foreach (var p in leftPiecesFinal.Skip(1)) p.Hide();
    foreach (var p in rightPiecesFinal)        p.Hide();

    piece.Color      = pieceColors[cutIdx];
    piece.FillColor  = pieceFills[cutIdx];
    piece.LineWeight = 2;
    piece.Name       = $"piece_{cutIdx}";

    animator.AddToAnimations(new DrawAnimation(piece, 0.7));

    VizConsole.Log($"Cut #{cutIdx}: x={finalCut:F1}, piece area={Math.Abs(piece.Area):F0} (target {targetArea:F0})");

    remaining = rightPiecesFinal[0];
}

// ---------- FINAL PIECE ----------
remaining.Color      = pieceColors[N - 1];
remaining.FillColor  = pieceFills[N - 1];
remaining.LineWeight = 2;
remaining.Name       = "piece_final";
animator.AddToAnimations(new DrawAnimation(remaining, 0.7));

VizConsole.Log($"Piece #{N-1} (final): area={Math.Abs(remaining.Area):F0}");

// ---------- STATUS ----------
var status = new VText(-260, -240,
    $"parcel: {totalArea:F0}u²  ·  {N} pieces  ·  target/piece: {targetArea:F0}u²  ·  {iters} bisection iters/cut",
    10) { Color = "Gold", Name = "status" };

animator.Animate();
        }
    }
}