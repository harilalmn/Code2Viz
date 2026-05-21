using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Animator.Canvas;
using Animator.Compiler;
using Animator.Console;
using Animator.Sketching;
using Code2Viz.Export;

namespace Animator.Export;

/// <summary>
/// Drives a fresh compile-and-run of the user's sketch with deterministic virtual time
/// so each captured frame represents exactly 1/fps seconds, regardless of how fast the
/// host machine can actually render. After every TickFrame the canvas is rasterized to
/// a <see cref="RenderTargetBitmap"/> and handed off to a <see cref="GifEncoder"/> or
/// <see cref="VideoExporter"/>.
/// </summary>
public static class SketchExporter
{
    public enum Format { Gif, Mp4 }

    /// <summary>
    /// Returns true on success, false on compile failure. Runs entirely on the dispatcher
    /// thread (WPF rendering requires it); the caller should show a progress dialog while
    /// this runs.
    /// </summary>
    public static async Task<bool> ExportAsync(
        AnimCanvas canvas,
        string sketchSource,
        string sourceName,
        string outputPath,
        Format format,
        double duration,
        int fps,
        Action<int, int>? progress = null)
    {
        var compiler = new SketchCompiler();

        // CompileAndRunAsync stops any running sketch, compiles, and starts a new one
        // (Setup() runs as part of Start). We then drive frames manually.
        var compileResult = await compiler.CompileAndRunAsync(sketchSource, sourceName);
        if (!compileResult.Success) return false;

        // Stop the live tick — OnRendering in MainWindow won't call Tick() while looping=false.
        SketchRuntime.Instance.SetLoopingPublic(false);

        // Apply any Setup-time requests the sketch issued (Size, Background). Normally the
        // host's OnRendering loop consumes these each frame; we disabled that above, so
        // drain them once before the capture loop or the canvas will keep its previous
        // zoom/background and only part of the sketch will end up in the bitmap.
        if (SketchRuntime.Instance.TryConsumeZoomRequest(out var zw, out var zh))
            canvas.SetBoundary(zw, zh);
        var bg = SketchRuntime.Instance.TryConsumeBackground();
        if (bg != null)
        {
            try { canvas.CanvasBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)); }
            catch { /* invalid color name — leave canvas background unchanged */ }
        }

        // Flush layout so any boundary-driven ZoomToBounds settles into ActualWidth/Height
        // before we sample them.
        canvas.UpdateLayout();
        canvas.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

        double dipWidth = canvas.ActualWidth;
        double dipHeight = canvas.ActualHeight;
        if (dipWidth <= 0 || dipHeight <= 0)
        {
            ConsoleOutput.Instance.WriteError("Export",
                $"Canvas has zero size ({dipWidth}x{dipHeight}); cannot export.");
            SketchRuntime.Instance.Stop();
            SketchRuntime.Instance.SetLoopingPublic(true);
            return false;
        }
        int width = (int)Math.Round(dipWidth);
        int height = (int)Math.Round(dipHeight);

        // Media Foundation (MP4) requires even dimensions
        if (format == Format.Mp4)
        {
            width -= width % 2;
            height -= height % 2;
        }

        int totalFrames = Math.Max(1, (int)(duration * fps));
        double dt = 1.0 / fps;

        try
        {
            if (format == Format.Gif)
            {
                using var fs = new FileStream(outputPath, FileMode.Create);
                using var encoder = new GifEncoder(fs, width, height, frameDelayMs: (int)(1000.0 / fps), repeat: true);
                for (int i = 0; i < totalFrames; i++)
                {
                    progress?.Invoke(i + 1, totalFrames);
                    SketchRuntime.Instance.TickFrame(i * dt, dt);
                    var rtb = CaptureCanvas(canvas, dipWidth, dipHeight, width, height);
                    encoder.AddFrame(rtb);
                }
            }
            else
            {
                using var encoder = new VideoExporter(outputPath, width, height, fps);
                for (int i = 0; i < totalFrames; i++)
                {
                    progress?.Invoke(i + 1, totalFrames);
                    SketchRuntime.Instance.TickFrame(i * dt, dt);
                    var rtb = CaptureCanvas(canvas, dipWidth, dipHeight, width, height);
                    encoder.AddFrame(rtb);
                }
                // VideoExporter.Dispose finalizes the sink writer.
            }
            return true;
        }
        finally
        {
            // Tear down the export sketch so the runtime is back to "no active sketch".
            SketchRuntime.Instance.Stop();
            SketchRuntime.Instance.SetLoopingPublic(true);
        }
    }

    private static RenderTargetBitmap CaptureCanvas(
        AnimCanvas canvas, double dipWidth, double dipHeight, int pixelWidth, int pixelHeight)
    {
        // The TickFrame call above fired FrameProduced → canvas.SetShapes → Refresh, which
        // re-rendered the canvas's DrawingVisual. Flush layout + render queues so the
        // updated visual is committed before we rasterize.
        canvas.UpdateLayout();
        canvas.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

        // Use a VisualBrush wrapping the live canvas's internal DrawingVisual, drawn into a
        // fresh DrawingVisual sized at the target pixel dimensions. By targeting the DrawingVisual
        // directly (instead of the canvas framework element), we avoid the parent layout grid
        // offset (which shifts column-positioned elements).
        // By leaving Viewbox/Viewport at their relative defaults, WPF automatically maps 100%
        // of the canvas drawing bounds to the output rectangle, bypassing DPI-dependent scaling bugs.
        var brush = new VisualBrush(canvas.DrawingVisual)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(brush, null, new Rect(0, 0, pixelWidth, pixelHeight));
        }

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        return rtb;
    }
}
