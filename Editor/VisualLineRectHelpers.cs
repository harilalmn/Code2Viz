using System.Windows;
using ICSharpCode.AvalonEdit.Rendering;

namespace Code2Viz.Editor;

internal static class VisualLineRectHelpers
{
    // A line that has a CodeLensElement gets a visual height of 2 * DefaultLineHeight
    // (the upper half is the "0 references" label area; the actual code text is
    // baselined at the bottom). BackgroundGeometryBuilder.GetRectsForSegment returns
    // rects spanning the full visual line, so any background highlight on such a line
    // would otherwise paint two text-rows tall.
    //
    // ClampToTextRow keeps the rect's bottom edge and crops the top so it covers only
    // the actual text row (DefaultLineHeight). Rects that are already line-height-sized
    // are returned unchanged, so the helper is a no-op on lines without a CodeLens.
    public static Rect ClampToTextRow(Rect rect, TextView textView)
    {
        double h = textView.DefaultLineHeight;
        if (rect.Height <= h + 0.5) return rect;
        return new Rect(rect.X, rect.Bottom - h, rect.Width, h);
    }
}
