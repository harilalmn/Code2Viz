using System;
using System.Collections.Generic;

// Pure-data generators for procedural art shapes.
// Returns coordinate tuples — no Code2Viz shape objects created here.
// StartViz.cs builds the actual shapes from these tuples.
public static class ProcArt
{
    // ===== L-SYSTEM FRACTAL TREE (binary, recursive) =====
    // Returns line segments in pre-order DFS so the tree "grows" from trunk outward.
    public static List<(double x1, double y1, double x2, double y2, int depth)>
        Tree(double rootX, double rootY, double initAngleDeg, double initLen,
             int maxDepth, double branchAngleDeg, double lenScale)
    {
        var lines = new List<(double, double, double, double, int)>();
        TreeRec(rootX, rootY,
                initAngleDeg * Math.PI / 180.0, initLen,
                maxDepth, 0,
                branchAngleDeg * Math.PI / 180.0, lenScale,
                lines);
        return lines;
    }

    static void TreeRec(double x, double y, double angle, double len,
                        int remaining, int depth,
                        double branchAng, double lenScale,
                        List<(double, double, double, double, int)> lines)
    {
        if (remaining <= 0) return;
        double ex = x + Math.Cos(angle) * len;
        double ey = y + Math.Sin(angle) * len;
        lines.Add((x, y, ex, ey, depth));
        TreeRec(ex, ey, angle + branchAng, len * lenScale,
                remaining - 1, depth + 1, branchAng, lenScale, lines);
        TreeRec(ex, ey, angle - branchAng, len * lenScale,
                remaining - 1, depth + 1, branchAng, lenScale, lines);
    }

    // ===== PENROSE P3 (Robinson-triangle deflation) =====
    // color 0 = thick half-rhombus (golden gnomon acute, 36° apex)
    // color 1 = thin half-rhombus (golden gnomon obtuse, 108° apex)
    // Initial: 10 thick triangles around center (the "sun"), alternating mirrored.
    public static List<(int color, double ax, double ay, double bx, double by, double cx, double cy)>
        Penrose(double centerX, double centerY, double radius, int depth)
    {
        double phi = (1.0 + Math.Sqrt(5.0)) / 2.0;
        var tris = new List<(int, double, double, double, double, double, double)>();

        for (int i = 0; i < 10; i++)
        {
            double a1 = (2 * i - 1) * Math.PI / 10.0;
            double a2 = (2 * i + 1) * Math.PI / 10.0;
            double bx = centerX + Math.Cos(a1) * radius;
            double by = centerY + Math.Sin(a1) * radius;
            double cx = centerX + Math.Cos(a2) * radius;
            double cy = centerY + Math.Sin(a2) * radius;
            if (i % 2 == 0)
            {
                double tmpx = bx, tmpy = by;
                bx = cx; by = cy;
                cx = tmpx; cy = tmpy;
            }
            tris.Add((0, centerX, centerY, bx, by, cx, cy));
        }

        for (int d = 0; d < depth; d++)
        {
            var next = new List<(int, double, double, double, double, double, double)>();
            foreach (var (col, ax, ay, bx, by, cx, cy) in tris)
            {
                if (col == 0)
                {
                    // thick: place P on AB at |AB|/phi from A; emit (0,C,P,B), (1,P,C,A)
                    double px = ax + (bx - ax) / phi;
                    double py = ay + (by - ay) / phi;
                    next.Add((0, cx, cy, px, py, bx, by));
                    next.Add((1, px, py, cx, cy, ax, ay));
                }
                else
                {
                    // thin: Q on BA at |BA|/phi, R on BC at |BC|/phi
                    // emit (1,R,C,A), (1,Q,R,B), (0,R,Q,A)
                    double qx = bx + (ax - bx) / phi;
                    double qy = by + (ay - by) / phi;
                    double rx = bx + (cx - bx) / phi;
                    double ry = by + (cy - by) / phi;
                    next.Add((1, rx, ry, cx, cy, ax, ay));
                    next.Add((1, qx, qy, rx, ry, bx, by));
                    next.Add((0, rx, ry, qx, qy, ax, ay));
                }
            }
            tris = next;
        }
        return tris;
    }

    // ===== HILBERT SPACE-FILLING CURVE (rotation-encoded recursion) =====
    // Order N → 4^N points. The curve is C0 continuous and recursive.
    public static List<(double x, double y)> Hilbert(double originX, double originY,
                                                     double size, int order)
    {
        var pts = new List<(double, double)>();
        HilbertRec(order, originX, originY, size, 0, 0, size, pts);
        return pts;
    }

    static void HilbertRec(int n, double x, double y,
                           double xi, double xj, double yi, double yj,
                           List<(double, double)> pts)
    {
        if (n <= 0)
        {
            pts.Add((x + (xi + yi) / 2.0, y + (xj + yj) / 2.0));
            return;
        }
        HilbertRec(n - 1, x, y, yi / 2.0, yj / 2.0, xi / 2.0, xj / 2.0, pts);
        HilbertRec(n - 1, x + xi / 2.0, y + xj / 2.0,
                   xi / 2.0, xj / 2.0, yi / 2.0, yj / 2.0, pts);
        HilbertRec(n - 1, x + xi / 2.0 + yi / 2.0, y + xj / 2.0 + yj / 2.0,
                   xi / 2.0, xj / 2.0, yi / 2.0, yj / 2.0, pts);
        HilbertRec(n - 1, x + xi / 2.0 + yi, y + xj / 2.0 + yj,
                   -yi / 2.0, -yj / 2.0, -xi / 2.0, -xj / 2.0, pts);
    }

    // ===== TRUCHET TILES =====
    // Each cell gets two diagonal quarter-arcs in one of two orientations.
    // Arcs always start/end at edge midpoints so they connect across cells.
    public static List<((double cx, double cy, double r, double s, double e) a1,
                        (double cx, double cy, double r, double s, double e) a2)>
        Truchet(double centerX, double centerY, double extent, int gridSize, int seed)
    {
        var tiles = new List<((double, double, double, double, double),
                              (double, double, double, double, double))>();
        double cs = extent / gridSize;
        double x0 = centerX - extent / 2.0;
        double y0 = centerY - extent / 2.0;
        var rng = new Random(seed);

        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                double tx = x0 + i * cs;
                double ty = y0 + j * cs;
                bool flip = rng.Next(2) == 0;
                if (flip)
                {
                    // arcs at BR and TL corners
                    tiles.Add((
                        (tx + cs, ty, cs / 2.0, 90.0, 180.0),
                        (tx, ty + cs, cs / 2.0, 270.0, 360.0)
                    ));
                }
                else
                {
                    // arcs at BL and TR corners
                    tiles.Add((
                        (tx, ty, cs / 2.0, 0.0, 90.0),
                        (tx + cs, ty + cs, cs / 2.0, 180.0, 270.0)
                    ));
                }
            }
        }
        return tiles;
    }
}
