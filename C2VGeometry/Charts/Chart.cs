using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C2VGeometry;

/// <summary>
/// Helper class for building Chart.js-style charts out of standard C2VGeometry primitives.
/// Each method returns a <see cref="VGroup"/> that contains all axis, label, gridline and
/// data shapes. Child shapes do not auto-register with the canvas individually; only the
/// returned group is registered, so callers can <c>Move</c>/<c>Rotate</c>/<c>ApplyStyle</c>
/// the chart as a single entity.
/// </summary>
public static class Chart
{
    /// <summary>
    /// Renders a bar chart with the given category labels and values.
    /// </summary>
    public static VGroup Bar(string[] labels, double[] values, ChartOptions? options = null)
    {
        if (labels == null || values == null) throw new ArgumentNullException();
        if (labels.Length != values.Length) throw new ArgumentException("labels and values must be the same length");
        options ??= new ChartOptions();

        return BuildGroup(opts: options, build: children =>
        {
            double yMin = options.YMin ?? Math.Min(0, values.DefaultIfEmpty(0).Min());
            double yMax = options.YMax ?? Math.Max(0, values.DefaultIfEmpty(0).Max());
            if (yMin == yMax) yMax = yMin + 1;
            var (nyMin, nyMax, yStep) = NiceRange(yMin, yMax, options.YTickCount);

            AddPlotFrame(children, options, hasXTicks: false, yMin: nyMin, yMax: nyMax, yStep: yStep);

            // Category X labels
            int n = labels.Length;
            double slotW = options.Width / Math.Max(n, 1);
            double barW = slotW * 0.7;
            double baselineY = MapY(Math.Max(0, nyMin), options, nyMin, nyMax);

            for (int i = 0; i < n; i++)
            {
                double cx = options.Origin.X + (i + 0.5) * slotW;
                double v = values[i];
                double topY = MapY(v, options, nyMin, nyMax);
                double y0 = Math.Min(baselineY, topY);
                double y1 = Math.Max(baselineY, topY);

                var bar = new VRectangle(new VXYZ(cx - barW / 2, y0), barW, y1 - y0);
                bar.Color = options.Palette[i % options.Palette.Length];
                bar.FillColor = options.Palette[i % options.Palette.Length];
                bar.Opacity = 0.85;
                children.Add(bar);

                // Category label below axis
                var labelPos = new VXYZ(cx, options.Origin.Y - options.LabelFontSize * 0.6);
                var lbl = new VText(labelPos, labels[i], options.LabelFontSize)
                {
                    Anchor = options.XLabelRotation == 0 ? VTextAnchor.TopCenter : VTextAnchor.TopRight,
                    Angle = options.XLabelRotation,
                    Color = options.TextColor
                };
                children.Add(lbl);
            }
        });
    }

    /// <summary>
    /// Renders a line chart from parallel x/y arrays.
    /// </summary>
    public static VGroup Line(double[] xs, double[] ys, ChartOptions? options = null)
    {
        if (xs == null || ys == null) throw new ArgumentNullException();
        if (xs.Length != ys.Length) throw new ArgumentException("xs and ys must be the same length");
        options ??= new ChartOptions();

        return BuildGroup(opts: options, build: children =>
        {
            var (nxMin, nxMax, xStep) = NiceRange(
                options.XMin ?? xs.DefaultIfEmpty(0).Min(),
                options.XMax ?? xs.DefaultIfEmpty(1).Max(),
                options.XTickCount);
            var (nyMin, nyMax, yStep) = NiceRange(
                options.YMin ?? ys.DefaultIfEmpty(0).Min(),
                options.YMax ?? ys.DefaultIfEmpty(1).Max(),
                options.YTickCount);

            AddPlotFrame(children, options, hasXTicks: true,
                xMin: nxMin, xMax: nxMax, xStep: xStep,
                yMin: nyMin, yMax: nyMax, yStep: yStep);

            // The line itself
            var pts = new List<VXYZ>(xs.Length);
            for (int i = 0; i < xs.Length; i++)
                pts.Add(new VXYZ(MapX(xs[i], options, nxMin, nxMax), MapY(ys[i], options, nyMin, nyMax)));
            if (pts.Count >= 2)
            {
                var poly = new VPolyline(pts);
                poly.Color = options.Palette[0];
                poly.LineWeight = 2.0;
                children.Add(poly);
            }

            // Point markers
            foreach (var p in pts)
            {
                var dot = new VCircle(p, 3.0);
                dot.Color = options.Palette[0];
                dot.FillColor = options.Palette[0];
                children.Add(dot);
            }
        });
    }

    /// <summary>
    /// Renders a scatter plot from a list of points.
    /// </summary>
    public static VGroup Scatter(VXYZ[] points, ChartOptions? options = null)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        options ??= new ChartOptions();

        return BuildGroup(opts: options, build: children =>
        {
            double dXMin = points.Length == 0 ? 0 : points.Min(p => p.X);
            double dXMax = points.Length == 0 ? 1 : points.Max(p => p.X);
            double dYMin = points.Length == 0 ? 0 : points.Min(p => p.Y);
            double dYMax = points.Length == 0 ? 1 : points.Max(p => p.Y);

            var (nxMin, nxMax, xStep) = NiceRange(options.XMin ?? dXMin, options.XMax ?? dXMax, options.XTickCount);
            var (nyMin, nyMax, yStep) = NiceRange(options.YMin ?? dYMin, options.YMax ?? dYMax, options.YTickCount);

            AddPlotFrame(children, options, hasXTicks: true,
                xMin: nxMin, xMax: nxMax, xStep: xStep,
                yMin: nyMin, yMax: nyMax, yStep: yStep);

            foreach (var p in points)
            {
                var px = MapX(p.X, options, nxMin, nxMax);
                var py = MapY(p.Y, options, nyMin, nyMax);
                var dot = new VCircle(new VXYZ(px, py), 4.0);
                dot.Color = options.Palette[0];
                dot.FillColor = options.Palette[0];
                dot.Opacity = 0.8;
                children.Add(dot);
            }
        });
    }

    /// <summary>
    /// Renders a pie chart of the given values. Optional labels are rendered at each slice's
    /// outer edge. Sectors are approximated as polygons (no <c>VSector</c> shape is needed).
    /// </summary>
    public static VGroup Pie(double[] values, string[]? labels = null, ChartOptions? options = null)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        options ??= new ChartOptions();

        return BuildGroup(opts: options, build: children =>
        {
            double total = values.Sum(v => Math.Max(0, v));
            if (total <= 0) return;

            // No axes for pie; just title + slices + optional labels
            AddTitle(children, options);

            double cx = options.Origin.X + options.Width / 2;
            double cy = options.Origin.Y + options.Height / 2;
            double r = 0.4 * Math.Min(options.Width, options.Height);
            var center = new VXYZ(cx, cy);

            double startDeg = 90; // start at top (12 o'clock), go clockwise
            for (int i = 0; i < values.Length; i++)
            {
                double v = Math.Max(0, values[i]);
                if (v <= 0) continue;
                double sweepDeg = 360.0 * v / total;
                double endDeg = startDeg - sweepDeg; // clockwise

                var sector = BuildSector(center, r, startDeg, endDeg);
                sector.Color = options.Palette[i % options.Palette.Length];
                sector.FillColor = options.Palette[i % options.Palette.Length];
                sector.Opacity = 0.85;
                children.Add(sector);

                // Slice label at slice centroid (just outside the radius)
                if (labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]))
                {
                    double midDeg = (startDeg + endDeg) / 2;
                    double rad = midDeg * Math.PI / 180.0;
                    double lx = cx + (r + options.LabelFontSize) * Math.Cos(rad);
                    double ly = cy + (r + options.LabelFontSize) * Math.Sin(rad);
                    var label = new VText(new VXYZ(lx, ly), labels[i], options.LabelFontSize)
                    {
                        Anchor = VTextAnchor.MiddleCenter,
                        Color = options.TextColor
                    };
                    children.Add(label);
                }

                startDeg = endDeg;
            }
        });
    }

    /// <summary>
    /// Renders a filled area chart from parallel x/y arrays.
    /// </summary>
    public static VGroup Area(double[] xs, double[] ys, ChartOptions? options = null)
    {
        if (xs == null || ys == null) throw new ArgumentNullException();
        if (xs.Length != ys.Length) throw new ArgumentException("xs and ys must be the same length");
        options ??= new ChartOptions();

        return BuildGroup(opts: options, build: children =>
        {
            var (nxMin, nxMax, xStep) = NiceRange(
                options.XMin ?? xs.DefaultIfEmpty(0).Min(),
                options.XMax ?? xs.DefaultIfEmpty(1).Max(),
                options.XTickCount);
            var (nyMin, nyMax, yStep) = NiceRange(
                options.YMin ?? Math.Min(0, ys.DefaultIfEmpty(0).Min()),
                options.YMax ?? ys.DefaultIfEmpty(1).Max(),
                options.YTickCount);

            AddPlotFrame(children, options, hasXTicks: true,
                xMin: nxMin, xMax: nxMax, xStep: xStep,
                yMin: nyMin, yMax: nyMax, yStep: yStep);

            if (xs.Length < 2) return;

            double baseY = MapY(Math.Max(0, nyMin), options, nyMin, nyMax);

            var polyPts = new List<VXYZ>(xs.Length + 2);
            polyPts.Add(new VXYZ(MapX(xs[0], options, nxMin, nxMax), baseY));
            for (int i = 0; i < xs.Length; i++)
                polyPts.Add(new VXYZ(MapX(xs[i], options, nxMin, nxMax), MapY(ys[i], options, nyMin, nyMax)));
            polyPts.Add(new VXYZ(MapX(xs[xs.Length - 1], options, nxMin, nxMax), baseY));

            var area = new VPolygon(polyPts);
            area.Color = options.Palette[0];
            area.FillColor = options.Palette[0];
            area.Opacity = 0.4;
            children.Add(area);

            // Stroke the top edge crisply
            var stroke = new List<VXYZ>(xs.Length);
            for (int i = 0; i < xs.Length; i++)
                stroke.Add(new VXYZ(MapX(xs[i], options, nxMin, nxMax), MapY(ys[i], options, nyMin, nyMax)));
            var line = new VPolyline(stroke);
            line.Color = options.Palette[0];
            line.LineWeight = 2.0;
            children.Add(line);
        });
    }

    // ------- internals -------

    /// <summary>
    /// Suppresses auto-registration while children are built, then registers the
    /// outer group exactly once (matching whatever the auto-register state was at entry).
    /// </summary>
    private static VGroup BuildGroup(ChartOptions opts, Action<List<Shape>> build)
    {
        bool prevAuto = Shape.AutoRegister;
        Shape.AutoRegister = false;
        VGroup group;
        try
        {
            var children = new List<Shape>();
            build(children);
            group = new VGroup(children);
        }
        finally
        {
            Shape.AutoRegister = prevAuto;
        }
        if (prevAuto && Shape.DefaultRegistry != null)
            Shape.DefaultRegistry.Register(group);
        return group;
    }

    private static void AddTitle(List<Shape> children, ChartOptions o)
    {
        if (!string.IsNullOrEmpty(o.Title))
        {
            var titlePos = new VXYZ(o.Origin.X + o.Width / 2, o.Origin.Y + o.Height + o.TitleFontSize * 0.6);
            children.Add(new VText(titlePos, o.Title, o.TitleFontSize)
            {
                Anchor = VTextAnchor.BottomCenter,
                FontWeight = VFontWeight.Bold,
                Color = o.TextColor
            });
        }
    }

    /// <summary>
    /// Adds axes, gridlines, tick labels, axis titles and chart title to <paramref name="children"/>.
    /// Pass <paramref name="hasXTicks"/>=false for category-axis charts (Bar) where the caller
    /// emits its own X labels.
    /// </summary>
    private static void AddPlotFrame(List<Shape> children, ChartOptions o, bool hasXTicks,
        double yMin, double yMax, double yStep,
        double xMin = 0, double xMax = 0, double xStep = 0)
    {
        double x0 = o.Origin.X, x1 = o.Origin.X + o.Width;
        double y0 = o.Origin.Y, y1 = o.Origin.Y + o.Height;

        // Gridlines (drawn first so they sit behind everything else)
        if (o.ShowGrid)
        {
            for (double yv = yMin; yv <= yMax + yStep * 0.5; yv += yStep)
            {
                double yp = MapY(yv, o, yMin, yMax);
                var g = new VLine(new VXYZ(x0, yp), new VXYZ(x1, yp));
                g.Color = o.GridColor;
                g.LineWeight = 0.5;
                children.Add(g);
            }
            if (hasXTicks)
            {
                for (double xv = xMin; xv <= xMax + xStep * 0.5; xv += xStep)
                {
                    double xp = MapX(xv, o, xMin, xMax);
                    var g = new VLine(new VXYZ(xp, y0), new VXYZ(xp, y1));
                    g.Color = o.GridColor;
                    g.LineWeight = 0.5;
                    children.Add(g);
                }
            }
        }

        // Axes
        var xAxis = new VLine(new VXYZ(x0, y0), new VXYZ(x1, y0));
        xAxis.Color = o.AxisColor;
        children.Add(xAxis);

        var yAxis = new VLine(new VXYZ(x0, y0), new VXYZ(x0, y1));
        yAxis.Color = o.AxisColor;
        children.Add(yAxis);

        // Y tick marks + labels
        double tickLen = 4;
        for (double yv = yMin; yv <= yMax + yStep * 0.5; yv += yStep)
        {
            double yp = MapY(yv, o, yMin, yMax);
            var tick = new VLine(new VXYZ(x0 - tickLen, yp), new VXYZ(x0, yp));
            tick.Color = o.AxisColor;
            children.Add(tick);

            var label = new VText(new VXYZ(x0 - tickLen - 2, yp), FormatTick(yv, o), o.LabelFontSize)
            {
                Anchor = VTextAnchor.MiddleRight,
                Color = o.TextColor
            };
            children.Add(label);
        }

        // X tick marks + labels (only for numeric X axes)
        if (hasXTicks)
        {
            for (double xv = xMin; xv <= xMax + xStep * 0.5; xv += xStep)
            {
                double xp = MapX(xv, o, xMin, xMax);
                var tick = new VLine(new VXYZ(xp, y0 - tickLen), new VXYZ(xp, y0));
                tick.Color = o.AxisColor;
                children.Add(tick);

                var label = new VText(new VXYZ(xp, y0 - tickLen - 2), FormatTick(xv, o), o.LabelFontSize)
                {
                    Anchor = o.XLabelRotation == 0 ? VTextAnchor.TopCenter : VTextAnchor.TopRight,
                    Angle = o.XLabelRotation,
                    Color = o.TextColor
                };
                children.Add(label);
            }
        }

        // Axis titles
        if (!string.IsNullOrEmpty(o.XAxisTitle))
        {
            var pos = new VXYZ(x0 + o.Width / 2, y0 - o.LabelFontSize * 3.2);
            children.Add(new VText(pos, o.XAxisTitle!, o.LabelFontSize + 1)
            {
                Anchor = VTextAnchor.TopCenter,
                Color = o.TextColor
            });
        }
        if (!string.IsNullOrEmpty(o.YAxisTitle))
        {
            var pos = new VXYZ(x0 - o.LabelFontSize * 4.0, y0 + o.Height / 2);
            children.Add(new VText(pos, o.YAxisTitle!, o.LabelFontSize + 1)
            {
                Anchor = VTextAnchor.BottomCenter,
                Angle = 90,
                Color = o.TextColor
            });
        }

        AddTitle(children, o);
    }

    private static string FormatTick(double v, ChartOptions o)
    {
        if (o.TickDecimalPlaces is int dp)
            return v.ToString("F" + dp, CultureInfo.InvariantCulture);
        // Auto: strip trailing zeros, but keep small fractions readable
        if (Math.Abs(v) >= 1e6 || (v != 0 && Math.Abs(v) < 1e-3))
            return v.ToString("G3", CultureInfo.InvariantCulture);
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double MapX(double v, ChartOptions o, double xMin, double xMax)
    {
        if (xMax == xMin) return o.Origin.X;
        return o.Origin.X + (v - xMin) / (xMax - xMin) * o.Width;
    }

    private static double MapY(double v, ChartOptions o, double yMin, double yMax)
    {
        if (yMax == yMin) return o.Origin.Y;
        return o.Origin.Y + (v - yMin) / (yMax - yMin) * o.Height;
    }

    /// <summary>
    /// Builds a pie-slice polygon from <paramref name="startDeg"/> down to <paramref name="endDeg"/>
    /// (degrees, mathematical convention: 0 = +X, 90 = +Y). Sweep is the unsigned arc length.
    /// </summary>
    private static VPolygon BuildSector(VXYZ center, double radius, double startDeg, double endDeg)
    {
        double sweep = Math.Abs(endDeg - startDeg);
        int segments = Math.Max(8, (int)Math.Ceiling(sweep / 4.0)); // 4° per segment
        var pts = new List<VXYZ>(segments + 2);
        pts.Add(center);
        double step = (endDeg - startDeg) / segments;
        for (int i = 0; i <= segments; i++)
        {
            double ang = (startDeg + step * i) * Math.PI / 180.0;
            pts.Add(new VXYZ(center.X + radius * Math.Cos(ang), center.Y + radius * Math.Sin(ang)));
        }
        return new VPolygon(pts);
    }

    /// <summary>
    /// "Nice numbers" axis range — picks rounded min/max/step that bracket the data.
    /// Same algorithm Chart.js / D3 use.
    /// </summary>
    private static (double niceMin, double niceMax, double step) NiceRange(double dataMin, double dataMax, int tickCount)
    {
        if (tickCount < 2) tickCount = 2;
        if (dataMin == dataMax)
        {
            // Degenerate: fabricate a small range around the value
            double pad = dataMin == 0 ? 1 : Math.Abs(dataMin) * 0.1;
            dataMin -= pad;
            dataMax += pad;
        }
        double range = NiceNumber(dataMax - dataMin, round: false);
        double step = NiceNumber(range / (tickCount - 1), round: true);
        double niceMin = Math.Floor(dataMin / step) * step;
        double niceMax = Math.Ceiling(dataMax / step) * step;
        return (niceMin, niceMax, step);
    }

    private static double NiceNumber(double x, bool round)
    {
        if (x <= 0) return 1;
        double exponent = Math.Floor(Math.Log10(x));
        double fraction = x / Math.Pow(10, exponent);
        double niceFraction = round
            ? (fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10)
            : (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);
        return niceFraction * Math.Pow(10, exponent);
    }
}
