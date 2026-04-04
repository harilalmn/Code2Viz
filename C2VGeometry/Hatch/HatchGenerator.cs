using System;
using System.Collections.Generic;

namespace C2VGeometry;

/// <summary>
/// Generates hatch line segments from a HatchType pattern, clipped to a polygon boundary.
/// </summary>
public static class HatchGenerator
{
    /// <summary>
    /// Generates clipped hatch line segments for the given boundary and pattern.
    /// </summary>
    public static List<(VXYZ Start, VXYZ End)> Generate(
        List<VXYZ> boundary, HatchType pattern, double scale, double patternAngle)
    {
        var result = new List<(VXYZ Start, VXYZ End)>();
        if (boundary.Count < 3 || pattern.Lines.Count == 0) return result;

        // Calculate boundary bounding box
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var pt in boundary)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y > maxY) maxY = pt.Y;
        }

        double bboxDiagonal = Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        double centerX = (minX + maxX) / 2;
        double centerY = (minY + maxY) / 2;

        foreach (var line in pattern.Lines)
        {
            var lineAngle = line.Angle + patternAngle;
            double angleRad = lineAngle * Math.PI / 180.0;
            double cosA = Math.Cos(angleRad);
            double sinA = Math.Sin(angleRad);

            // The spacing between parallel lines is deltaY * scale
            double spacing = Math.Abs(line.DeltaY) * scale;
            if (spacing < 1e-10) spacing = 0.125 * scale; // fallback for zero spacing

            // Shift along the line direction (deltaX)
            double shiftAlongLine = line.DeltaX * scale;

            // Origin in world coordinates, scaled and rotated
            double ox = line.OriginX * scale;
            double oy = line.OriginY * scale;

            // Rotate origin by patternAngle
            if (Math.Abs(patternAngle) > 1e-9)
            {
                double paRad = patternAngle * Math.PI / 180.0;
                double cosPa = Math.Cos(paRad);
                double sinPa = Math.Sin(paRad);
                double rx = ox * cosPa - oy * sinPa;
                double ry = ox * sinPa + oy * cosPa;
                ox = rx;
                oy = ry;
            }

            // Direction along the line and perpendicular
            double dirX = cosA;
            double dirY = sinA;
            double perpX = -sinA;
            double perpY = cosA;

            // Calculate how many parallel lines we need
            // Project boundary onto the perpendicular direction
            double minPerp = double.MaxValue;
            double maxPerp = double.MinValue;
            foreach (var pt in boundary)
            {
                double proj = (pt.X - ox) * perpX + (pt.Y - oy) * perpY;
                if (proj < minPerp) minPerp = proj;
                if (proj > maxPerp) maxPerp = proj;
            }

            int minIndex = (int)Math.Floor(minPerp / spacing) - 1;
            int maxIndex = (int)Math.Ceiling(maxPerp / spacing) + 1;

            // Limit line count to prevent runaway generation
            int lineCount = maxIndex - minIndex;
            if (lineCount > 10000) continue;

            // Scale dash pattern
            double[]? dashes = line.Dashes;
            double[]? scaledDashes = null;
            double dashPatternLen = 0;
            if (dashes != null && dashes.Length > 0)
            {
                scaledDashes = new double[dashes.Length];
                for (int d = 0; d < dashes.Length; d++)
                {
                    scaledDashes[d] = dashes[d] * scale;
                    dashPatternLen += Math.Abs(scaledDashes[d]);
                }
            }

            for (int i = minIndex; i <= maxIndex; i++)
            {
                double perpDist = i * spacing;

                // Line origin for this parallel line
                double lx = ox + perpDist * perpX + i * shiftAlongLine * dirX;
                double ly = oy + perpDist * perpY + i * shiftAlongLine * dirY;

                // Extend line well beyond bounding box
                double halfExtent = bboxDiagonal;
                double x0 = lx - halfExtent * dirX;
                double y0 = ly - halfExtent * dirY;
                double x1 = lx + halfExtent * dirX;
                double y1 = ly + halfExtent * dirY;

                // Clip line to polygon and get visible segments
                var segments = ClipLineToPolygon(x0, y0, x1, y1, boundary);

                if (scaledDashes != null && scaledDashes.Length > 0)
                {
                    // Apply dash pattern to each clipped segment
                    foreach (var seg in segments)
                    {
                        var dashedSegs = ApplyDashPattern(seg.sx, seg.sy, seg.ex, seg.ey, lx, ly, dirX, dirY, scaledDashes, dashPatternLen);
                        foreach (var ds in dashedSegs)
                        {
                            result.Add((new VXYZ(ds.sx, ds.sy), new VXYZ(ds.ex, ds.ey)));
                        }
                    }
                }
                else
                {
                    // Continuous line
                    foreach (var seg in segments)
                    {
                        result.Add((new VXYZ(seg.sx, seg.sy), new VXYZ(seg.ex, seg.ey)));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clips an infinite line segment to a polygon boundary using scanline intersection.
    /// Returns sorted visible segments inside the polygon.
    /// </summary>
    private static List<(double sx, double sy, double ex, double ey)> ClipLineToPolygon(
        double x0, double y0, double x1, double y1, List<VXYZ> polygon)
    {
        var result = new List<(double sx, double sy, double ex, double ey)>();

        double dx = x1 - x0;
        double dy = y1 - y0;
        double lineLen = Math.Sqrt(dx * dx + dy * dy);
        if (lineLen < 1e-12) return result;

        // Find all intersections with polygon edges
        var tValues = new List<double>();
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double ex = polygon[j].X - polygon[i].X;
            double ey = polygon[j].Y - polygon[i].Y;

            double denom = dx * ey - dy * ex;
            if (Math.Abs(denom) < 1e-12) continue;

            double fx = polygon[i].X - x0;
            double fy = polygon[i].Y - y0;

            double t = (fx * ey - fy * ex) / denom;
            double u = (fx * dy - fy * dx) / denom;

            if (u >= -1e-9 && u <= 1.0 + 1e-9)
            {
                tValues.Add(t);
            }
        }

        if (tValues.Count < 2) return result;

        tValues.Sort();

        // Pair up intersections - segments between odd pairs are inside
        for (int i = 0; i < tValues.Count - 1; i += 2)
        {
            double t1 = tValues[i];
            double t2 = tValues[i + 1];

            // Check midpoint is inside polygon
            double midT = (t1 + t2) / 2;
            double midX = x0 + midT * dx;
            double midY = y0 + midT * dy;

            if (IsPointInPolygon(midX, midY, polygon))
            {
                result.Add((x0 + t1 * dx, y0 + t1 * dy, x0 + t2 * dx, y0 + t2 * dy));
            }
        }

        // Handle case where odd intersection count leaves unpaired segments
        // Try adjacent pairs that might be inside
        if (result.Count == 0 && tValues.Count >= 2)
        {
            for (int i = 0; i < tValues.Count - 1; i++)
            {
                double t1 = tValues[i];
                double t2 = tValues[i + 1];
                double midT = (t1 + t2) / 2;
                double midX = x0 + midT * dx;
                double midY = y0 + midT * dy;
                if (IsPointInPolygon(midX, midY, polygon))
                {
                    result.Add((x0 + t1 * dx, y0 + t1 * dy, x0 + t2 * dx, y0 + t2 * dy));
                }
            }
        }

        return result;
    }

    private static bool IsPointInPolygon(double px, double py, List<VXYZ> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y > py) != (polygon[j].Y > py) &&
                px < (polygon[j].X - polygon[i].X) * (py - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    private static List<(double sx, double sy, double ex, double ey)> ApplyDashPattern(
        double sx, double sy, double ex, double ey,
        double originX, double originY, double dirX, double dirY,
        double[] dashes, double patternLen)
    {
        var result = new List<(double sx, double sy, double ex, double ey)>();

        double segDx = ex - sx;
        double segDy = ey - sy;
        double segLen = Math.Sqrt(segDx * segDx + segDy * segDy);
        if (segLen < 1e-10 || patternLen < 1e-10) return result;

        double ndx = segDx / segLen;
        double ndy = segDy / segLen;

        double startProj = (sx - originX) * dirX + (sy - originY) * dirY;

        double phase = startProj % patternLen;
        if (phase < 0) phase += patternLen;

        double accumulated = 0;
        int dashIndex = 0;
        double posInDash = 0;
        for (int d = 0; d < dashes.Length; d++)
        {
            double dl = Math.Abs(dashes[d]);
            if (accumulated + dl > phase + 1e-12)
            {
                dashIndex = d;
                posInDash = phase - accumulated;
                break;
            }
            accumulated += dl;
            if (d == dashes.Length - 1)
            {
                dashIndex = 0;
                posInDash = 0;
            }
        }

        double pos = 0;
        while (pos < segLen - 1e-10)
        {
            double dashVal = dashes[dashIndex % dashes.Length];
            double dashLen = Math.Abs(dashVal);
            bool isDraw = dashVal > 0;
            bool isDot = Math.Abs(dashVal) < 1e-10;

            double remaining = dashLen - posInDash;
            double advance = Math.Min(remaining, segLen - pos);

            if (isDot)
            {
                double dotX = sx + pos * ndx;
                double dotY = sy + pos * ndy;
                result.Add((dotX, dotY, dotX, dotY)); // zero-length = dot
                advance = Math.Min(0.001, segLen - pos); // minimal advance for dot
            }
            else if (isDraw && advance > 1e-10)
            {
                double dsx = sx + pos * ndx;
                double dsy = sy + pos * ndy;
                double dex = sx + (pos + advance) * ndx;
                double dey = sy + (pos + advance) * ndy;
                result.Add((dsx, dsy, dex, dey));
            }

            pos += advance;
            posInDash += advance;

            if (posInDash >= dashLen - 1e-10)
            {
                posInDash = 0;
                dashIndex = (dashIndex + 1) % dashes.Length;
            }
        }

        return result;
    }
}
