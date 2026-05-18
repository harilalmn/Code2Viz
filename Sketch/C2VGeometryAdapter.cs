using System.Linq;
using Code2Viz.Console;
using Geom = Code2Viz.Geometry;
using C2V = C2VGeometry;

namespace Code2Viz.Sketching;

/// <summary>
/// One-way converter from <see cref="C2VGeometry"/> shapes (computational, framework-agnostic)
/// to <see cref="Code2Viz.Geometry"/> shapes (WPF-renderable).
/// <para>
/// The WPF canvas's render switch is hardcoded for <c>Code2Viz.Geometry</c> types. Rather than
/// generalize the canvas, we copy each frame's shapes across the boundary. Unsupported types
/// log a console warning and are skipped — extend the switch as needed.
/// </para>
/// </summary>
public static class C2VGeometryAdapter
{
    /// <summary>Converts a single C2VGeometry shape to its WPF-renderable equivalent, or null if unsupported.</summary>
    public static Geom.IDrawable? Convert(C2V.Shape src) => src switch
    {
        C2V.VCircle c    => CopyStyle(c,    new Geom.VCircle(c.Center.X, c.Center.Y, c.Radius)),
        C2V.VLine l      => CopyStyle(l,    new Geom.VLine(ToVPoint(l.Start), ToVPoint(l.End))),
        C2V.VRectangle r => CopyStyle(r,    new Geom.VRectangle(ToVPoint(r.Corner), r.Width, r.Height) { RotationAngle = r.RotationAngle }),
        C2V.VEllipse e   => CopyStyle(e,    new Geom.VEllipse(ToVPoint(e.Center), e.RadiusX, e.RadiusY)),
        C2V.VArc a       => CopyStyle(a,    new Geom.VArc(ToVPoint(a.Center), a.Radius, a.StartAngle, a.EndAngle)),
        C2V.VPolygon pg  => CopyStyle(pg,   new Geom.VPolygon(pg.Points.Select(ToVPoint).ToArray())),
        C2V.VPolyline pl => CopyStyle(pl,   new Geom.VPolyline(pl.Points.Select(ToVPoint).ToArray())),
        C2V.VBezier b    => CopyStyle(b,    new Geom.VBezier(ToVPoint(b.P0), ToVPoint(b.P1), ToVPoint(b.P2), ToVPoint(b.P3))),
        C2V.VSpline sp   => CopyStyle(sp,   new Geom.VSpline(sp.ControlPoints.Select(ToVPoint).ToArray())),
        _ => LogUnsupported(src)
    };

    private static Geom.VPoint ToVPoint(C2V.VXYZ p) => new(p.X, p.Y);

    private static Geom.IDrawable CopyStyle<TSrc, TDst>(TSrc src, TDst dst)
        where TSrc : C2V.Shape
        where TDst : Geom.Shape
    {
        dst.Color = src.Color;
        dst.FillColor = src.FillColor;
        dst.LineWeight = src.LineWeight;
        dst.LineType = (Geom.LineType)(int)src.LineType;
        dst.LineTypeScale = src.LineTypeScale;
        dst.DrawFactor = src.DrawFactor;
        dst.OffsetX = src.OffsetX;
        dst.OffsetY = src.OffsetY;
        dst.Opacity = src.Opacity;
        dst.IsVisible = src.IsVisible;
        if (!string.IsNullOrEmpty(src.Name)) dst.Name = src.Name;
        return dst;
    }

    private static Geom.IDrawable? LogUnsupported(C2V.Shape s)
    {
        ConsoleOutput.Instance.WriteLine(
            "Sketch", 0,
            $"Warning: C2VGeometry type '{s.GetType().Name}' has no canvas adapter — skipped.");
        return null;
    }
}
