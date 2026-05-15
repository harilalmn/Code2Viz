using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Code2Viz.Geometry;
using Code2Viz.Console;
using Code2Viz.Animation;

namespace MyVizProject
{
    public class Viz
    {
        public static void Main()
        {
            // ============================================================
            // CONVEX HULL · ALGORITHM RACE  (sequential-only animator)
            // No parallel List<Animation> calls. Round-robin across panels.
            // ============================================================
            ShapeDefaults.Reset();

            // ---------- SETUP ----------
            var rand = new Random(42);
            int N = 15;
            var basePts = new List<VPoint>();
            for (int i = 0; i < N; i++)
            basePts.Add(new VPoint(rand.NextDouble() * 180 - 90, rand.NextDouble() * 180 - 90));

            var centers      = new[]
            { new VPoint(-300, 0), new VPoint(0, 0), new VPoint(300, 0)
            };
            var titles       = new[]
            { "GRAHAM SCAN", "JARVIS MARCH", "ANDREW'S MONOTONE"
            };
            var subs         = new[]
            { "stack on polar sort", "gift wrap brute force", "two monotone chains"
            };
            var complexities = new[]
            { "O(n log n)", "O(n · h)", "O(n log n)"
            };

            // Dim trail colors → hull's Lime stands out against them at full opacity
            var trailColA = new[]
            { "#7A6420", "#8A4858", "#3D6B7A"
            }; // primary trail per panel
            var trailColB = "#2C5260"; // Andrew upper-chain variant

            var panelPts = new List<VPoint>[3];
            for (int p = 0; p < 3; p++)
            {
                var c = centers[p];
                panelPts[p] = basePts.Select(pt => new VPoint(pt.X + c.X, pt.Y + c.Y)).ToList();
            }

            // ---------- STATIC CHROME ----------
            var masterTitle = new VText(-205, 235, "CONVEX HULL  ·  ALGORITHM RACE", 18)
            {
                Color = "Gold", FontWeight = VFontWeight.Bold
            };
            var masterSub = new VText(-178, 212, "same 15 points · three strategies · count the work", 10)
            {
                Color = "Silver"
            };

            // Legend
            var legHull     = new VLine(-380, -235, -355, -235)
            { Color = "Lime", LineWeight = 3
            };
            var legHullTxt  = new VText(-350, -239, "hull", 10)
            { Color = "Silver"
            };
            var legTrail    = new VLine(-300, -235, -275, -235)
            { Color = "Silver", LineType = LineType.Dashed, LineWeight = 1
            };
            var legTrailTxt = new VText(-270, -239, "test edge", 10)
            { Color = "Silver"
            };
            var legStart    = new VCircle(-195, -235, 4)
            { Color = "Gold", FillColor = "Gold"
            };
            var legStartTxt = new VText(-185, -239, "start vertex", 10)
            { Color = "Silver"
            };

            for (int p = 0; p < 3; p++)
            {
                var c = centers[p];
                var frame = new VRectangle(c.X - 135, c.Y - 135, 270, 270)
                {
                    Color = "#404060", LineType = LineType.Dashed, LineWeight = 1, LineTypeScale = 0.5
                };
                var tt = new VText(c.X - titles[p].Length * 3.5, c.Y + 152, titles[p], 13)
                {
                    Color = "Gold", FontWeight = VFontWeight.Bold
                };
                var st = new VText(c.X - subs[p].Length * 2.4, c.Y + 138, subs[p], 9)
                { Color = "DarkGray"
                };
                var cx = new VText(c.X - complexities[p].Length * 3.5, c.Y - 152, complexities[p], 12)
                {
                    Color = "Silver", FontWeight = VFontWeight.Bold
                };
                foreach (var pt in panelPts[p])
                {
                    var dot = new VCircle(pt.X, pt.Y, 2.5)
                    { Color = "Cyan", FillColor = "#1FAEC4"
                    };
                }
            }

            // ---------- ALGORITHM TRACES ----------
            var trails    = new List<VLine>[3];
            var hulls     = new VPolygon[3];
            var startDots = new VCircle[3];

            // Helper — trail VLine at full opacity (DrawAnimation handles reveal).
            // Name is set explicitly so HideUnnamedShapes() doesn't strip these
            // (the AnimationNameRewriter only names shapes assigned to `var x = new ...`).
            VLine T(VPoint a, VPoint b, string color, LineType lt, double weight)
            {
                return new VLine(a, b)
                { Color = color, LineType = lt, LineWeight = weight, Name = "trail"
                };
            }

            // ---- GRAHAM SCAN ----
            {
                var pts = panelPts[0];
                var trail = new List<VLine>();

                int pivotIdx = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    var pi = pts[i]; var pp = pts[pivotIdx];
                    if (pi.Y < pp.Y || (pi.Y == pp.Y && pi.X < pp.X)) pivotIdx = i;
                }
                var pivot = pts[pivotIdx];

                var others = new List<VPoint>();
                for (int i = 0; i < pts.Count; i++) if (i != pivotIdx) others.Add(pts[i]);
                others = others
                .OrderBy(p => Math.Atan2(p.Y - pivot.Y, p.X - pivot.X))
                .ThenBy(p => (p.X - pivot.X) * (p.X - pivot.X) + (p.Y - pivot.Y) * (p.Y - pivot.Y))
                .ToList();

                // Dotted = sort rays from pivot
                foreach (var p in others)
                trail.Add(T(pivot, p, trailColA[0], LineType.Dotted, 1.0));

                // Dashed = stack-scan decision edges
                var stack = new List<VPoint>
                { pivot, others[0]
                };
                for (int i = 1; i < others.Count; i++)
                {
                    var cand = others[i];
                    while (stack.Count >= 2)
                    {
                        var top  = stack[stack.Count - 1];
                        var prev = stack[stack.Count - 2];
                        double cr = (top.X - prev.X) * (cand.Y - prev.Y) - (top.Y - prev.Y) * (cand.X - prev.X);
                        trail.Add(T(top, cand, trailColA[0], LineType.Dashed, 1.3));
                        if (cr > 0) break;
                        stack.RemoveAt(stack.Count - 1);
                    }
                    stack.Add(cand);
                }

                trails[0] = trail;
                hulls[0]  = new VPolygon(stack)
                { Color = "Lime", FillColor = "Transparent", LineWeight = 3, Name = "hull0"
                };
                startDots[0] = new VCircle(pivot.X, pivot.Y, 5)
                { Color = "Gold", FillColor = "Gold", LineWeight = 1.5, Name = "start0"
                };
            }

            // ---- JARVIS MARCH ----
            {
                var pts = panelPts[1];
                var trail = new List<VLine>();

                int startIdx = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    var pi = pts[i]; var ps = pts[startIdx];
                    if (pi.X < ps.X || (pi.X == ps.X && pi.Y < ps.Y)) startIdx = i;
                }
                var start = pts[startIdx];
                var hullPts = new List<VPoint>();
                int currentIdx = startIdx;
                int safety = 0;
                do
                {
                    var current = pts[currentIdx];
                    hullPts.Add(current);
                    int nextIdx = (currentIdx + 1) % pts.Count;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if (i == currentIdx) continue;
                        var p = pts[i];
                        var next = pts[nextIdx];
                        trail.Add(T(current, p, trailColA[1], LineType.Dotted, 1.0));
                        double cr = (next.X - current.X) * (p.Y - current.Y) - (next.Y - current.Y) * (p.X - current.X);
                        double dC = (next.X - current.X) * (next.X - current.X) + (next.Y - current.Y) * (next.Y - current.Y);
                        double dP = (p.X - current.X) * (p.X - current.X) + (p.Y - current.Y) * (p.Y - current.Y);
                        if (cr < 0 || (Math.Abs(cr) < 1e-9 && dP > dC)) nextIdx = i;
                    }
                    currentIdx = nextIdx;
                    if (++safety > 200) break;
                } while (currentIdx != startIdx);

                trails[1] = trail;
                hulls[1]  = new VPolygon(hullPts)
                { Color = "Lime", FillColor = "Transparent", LineWeight = 3, Name = "hull1"
                };
                startDots[1] = new VCircle(start.X, start.Y, 5)
                { Color = "Gold", FillColor = "Gold", LineWeight = 1.5, Name = "start1"
                };
            }

            // ---- ANDREW'S MONOTONE CHAIN ----
            {
                var pts = panelPts[2];
                var trail = new List<VLine>();
                var sorted = pts.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                var H = new List<VPoint>();

                foreach (var p in sorted)
                {
                    while (H.Count >= 2)
                    {
                        var a = H[H.Count - 2]; var b = H[H.Count - 1];
                        double cr = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                        trail.Add(T(b, p, trailColA[2], LineType.Dashed, 1.3));
                        if (cr > 0) break;
                        H.RemoveAt(H.Count - 1);
                    }
                    H.Add(p);
                }
                int tt = H.Count + 1;
                for (int i = sorted.Count - 2; i >= 0; i--)
                {
                    var p = sorted[i];
                    while (H.Count >= tt)
                    {
                        var a = H[H.Count - 2]; var b = H[H.Count - 1];
                        double cr = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                        trail.Add(T(b, p, trailColB, LineType.DashDot, 1.3));
                        if (cr > 0) break;
                        H.RemoveAt(H.Count - 1);
                    }
                    H.Add(p);
                }
                H.RemoveAt(H.Count - 1);

                trails[2] = trail;
                hulls[2]  = new VPolygon(H)
                { Color = "Lime", FillColor = "Transparent", LineWeight = 3, Name = "hull2"
                };
                startDots[2] = new VCircle(sorted[0].X, sorted[0].Y, 5)
                { Color = "Gold", FillColor = "Gold", LineWeight = 1.5, Name = "start2"
                };
            }

            // ---------- COUNT LABELS (built but invisible until end) ----------
            var counts = new VText[3];
            var countCol = new[]
            { "#E8C547", "#FF8FA3", "#7ECEE8"
            };
            for (int p = 0; p < 3; p++)
            {
                counts[p] = new VText(centers[p].X - 50, centers[p].Y - 175,
                $"WORK: {trails[p].Count} tests", 13)
                {
                    Color = countCol[p], FontWeight = VFontWeight.Bold, Name = $"count{p}"
                };
            }

            // ---------- ANIMATION  (sequential round-robin) ----------
            var animator = new Animator();
            double trailStep = 0.04;
            double hullDur   = 1.2;

            // Round-robin interleave: panel0[i], panel1[i], panel2[i], panel0[i+1], ...
            // All single-shape AddToAnimations — no List<Animation> overloads.
            int maxSteps = Math.Max(trails[0].Count, Math.Max(trails[1].Count, trails[2].Count));
            for (int i = 0; i < maxSteps; i++)
            {
                for (int p = 0; p < 3; p++)
                if (i < trails[p].Count) animator.AddToAnimations(new DrawAnimation(trails[p][i], trailStep));
            }

            for (int p = 0; p < 3; p++)
            animator.AddToAnimations(new DrawAnimation(hulls[p], hullDur));

            for (int p = 0; p < 3; p++)
            animator.AddToAnimations(new DrawAnimation(counts[p], 0.5));

            animator.Animate();

            VizConsole.Log($"Graham:  {trails[0].Count} tests");
            VizConsole.Log($"Jarvis:  {trails[1].Count} tests");
            VizConsole.Log($"Andrew:  {trails[2].Count} tests");
            VizConsole.Log($"Total animation steps queued: {trails[0].Count + trails[1].Count + trails[2].Count + 6}");
        }
    }
}
