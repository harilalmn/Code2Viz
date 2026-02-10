using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Code2Viz.Editor.Minimap
{
    /// <summary>
    /// Represents an error/warning marker to display in the minimap.
    /// </summary>
    public class MinimapMarker
    {
        public int Line { get; set; }
        public Color Color { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// VSCode-style minimap control for the code editor.
    /// Displays a scaled view of the code with syntax highlighting and a viewport indicator.
    /// </summary>
    public partial class MinimapControl : UserControl
    {
        private TextEditor? _editor;
        private readonly MinimapRenderer _renderer;
        private readonly DispatcherTimer _renderTimer;
        private DrawingVisual? _cachedVisual;
        private bool _isDragging;
        private double _dragStartY;
        private double _dragStartScrollOffset;
        private List<MinimapMarker> _markers = new();

        /// <summary>
        /// Height per line in the minimap (pixels).
        /// </summary>
        public double LineHeight { get; set; } = 2.0;

        /// <summary>
        /// Width per character in the minimap (pixels).
        /// </summary>
        public double CharWidth { get; set; } = 1.0;

        /// <summary>
        /// Debounce delay for re-rendering after text changes (ms).
        /// </summary>
        public int RenderDebounceMs { get; set; } = 300;

        public MinimapControl()
        {
            InitializeComponent();

            _renderer = new MinimapRenderer();

            // Debounce timer for rendering
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(RenderDebounceMs)
            };
            _renderTimer.Tick += (s, e) =>
            {
                _renderTimer.Stop();
                RenderMinimap();
            };

            SizeChanged += (s, e) => UpdateViewportIndicator();
        }

        /// <summary>
        /// Attaches the minimap to a TextEditor.
        /// </summary>
        public void AttachToEditor(TextEditor editor)
        {
            if (_editor != null)
            {
                DetachFromEditor();
            }

            _editor = editor;

            // Subscribe to events
            _editor.TextChanged += Editor_TextChanged;
            _editor.TextArea.TextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
            _editor.TextArea.TextView.VisualLinesChanged += TextView_VisualLinesChanged;

            // Initial render
            ScheduleRender();
        }

        /// <summary>
        /// Detaches the minimap from the current editor.
        /// </summary>
        public void DetachFromEditor()
        {
            if (_editor != null)
            {
                _editor.TextChanged -= Editor_TextChanged;
                _editor.TextArea.TextView.ScrollOffsetChanged -= TextView_ScrollOffsetChanged;
                _editor.TextArea.TextView.VisualLinesChanged -= TextView_VisualLinesChanged;
                _editor = null;
            }
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            ScheduleRender();
        }

        private void TextView_ScrollOffsetChanged(object? sender, EventArgs e)
        {
            UpdateViewportIndicator();
        }

        private void TextView_VisualLinesChanged(object? sender, EventArgs e)
        {
            UpdateViewportIndicator();
        }

        /// <summary>
        /// Schedules a debounced re-render of the minimap.
        /// </summary>
        public void ScheduleRender()
        {
            _renderTimer.Stop();
            _renderTimer.Start();
        }

        /// <summary>
        /// Forces an immediate re-render of the minimap.
        /// </summary>
        public void ForceRender()
        {
            _renderTimer.Stop();
            RenderMinimap();
        }

        /// <summary>
        /// Updates the error/warning markers displayed in the minimap.
        /// </summary>
        /// <param name="markers">Collection of markers to display</param>
        public void UpdateMarkers(IEnumerable<MinimapMarker> markers)
        {
            _markers = markers.ToList();
            RenderMarkers();
        }

        /// <summary>
        /// Clears all error/warning markers from the minimap.
        /// </summary>
        public void ClearMarkers()
        {
            _markers.Clear();
            RenderMarkers();
        }

        private void RenderMinimap()
        {
            if (_editor == null) return;

            try
            {
                var code = _editor.Text;
                var maxWidth = ActualWidth > 0 ? ActualWidth : Width;

                // Render to DrawingVisual
                _cachedVisual = _renderer.Render(code, LineHeight, CharWidth, maxWidth);

                // Clear and add to canvas
                MinimapCanvas.Children.Clear();

                var host = new VisualHost(_cachedVisual);
                MinimapCanvas.Children.Add(host);

                // Render markers on top of code
                RenderMarkers();

                // Update viewport indicator
                UpdateViewportIndicator();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Minimap render error: {ex.Message}");
            }
        }

        /// <summary>
        /// Renders error/warning markers as colored rectangles on the right edge of the minimap.
        /// </summary>
        private void RenderMarkers()
        {
            // Remove existing marker rectangles (keep the code visual)
            var markersToRemove = MinimapCanvas.Children.OfType<Rectangle>().ToList();
            foreach (var marker in markersToRemove)
            {
                MinimapCanvas.Children.Remove(marker);
            }

            if (_markers.Count == 0) return;

            var markerWidth = 4.0;
            var markerHeight = Math.Max(LineHeight, 3.0); // At least 3px for visibility

            foreach (var marker in _markers)
            {
                var rect = new Rectangle
                {
                    Width = markerWidth,
                    Height = markerHeight,
                    Fill = new SolidColorBrush(marker.Color),
                    ToolTip = marker.Message,
                    Cursor = Cursors.Hand
                };

                // Position on the right edge
                var y = (marker.Line - 1) * LineHeight;
                WpfCanvas.SetLeft(rect, ActualWidth - markerWidth - 2);
                WpfCanvas.SetTop(rect, y);

                // Allow clicking on marker to navigate
                var line = marker.Line;
                rect.MouseLeftButtonDown += (s, e) =>
                {
                    NavigateToLine(line);
                    e.Handled = true;
                };

                MinimapCanvas.Children.Add(rect);
            }
        }

        /// <summary>
        /// Navigates the editor to the specified line.
        /// </summary>
        private void NavigateToLine(int line)
        {
            if (_editor == null) return;

            try
            {
                line = Math.Max(1, Math.Min(line, _editor.Document.LineCount));

                // Center the line in the viewport
                var editorLineHeight = _editor.TextArea.TextView.DefaultLineHeight;
                var viewportHeightInLines = _editor.TextArea.TextView.ActualHeight / editorLineHeight;

                var targetOffset = (line - viewportHeightInLines / 2) * editorLineHeight;
                targetOffset = Math.Max(0, targetOffset);

                _editor.ScrollToVerticalOffset(targetOffset);

                // Move caret to the line
                var lineInfo = _editor.Document.GetLineByNumber(line);
                _editor.CaretOffset = lineInfo.Offset;
                _editor.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate to line error: {ex.Message}");
            }
        }

        private void UpdateViewportIndicator()
        {
            if (_editor == null) return;

            try
            {
                var textView = _editor.TextArea.TextView;
                var document = _editor.Document;

                if (document.LineCount == 0) return;

                // Calculate total document height in minimap coordinates
                var totalMinimapHeight = document.LineCount * LineHeight;

                // Calculate viewport position and size
                var scrollOffset = textView.VerticalOffset;
                var editorLineHeight = textView.DefaultLineHeight;
                var visibleLines = textView.ActualHeight / editorLineHeight;

                // First visible line (0-based)
                var firstVisibleLine = scrollOffset / editorLineHeight;
                var viewportTop = firstVisibleLine * LineHeight;
                var viewportHeight = visibleLines * LineHeight;

                // Clamp values
                viewportTop = Math.Max(0, viewportTop);
                viewportHeight = Math.Max(20, Math.Min(viewportHeight, ActualHeight - viewportTop));

                // Update viewport indicator
                WpfCanvas.SetTop(ViewportIndicator, viewportTop);
                ViewportIndicator.Width = ActualWidth;
                ViewportIndicator.Height = viewportHeight;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Viewport update error: {ex.Message}");
            }
        }

        private void MinimapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_editor == null) return;

            var pos = e.GetPosition(MinimapCanvas);

            // Check if clicking on viewport indicator for dragging
            var viewportTop = WpfCanvas.GetTop(ViewportIndicator);
            var viewportBottom = viewportTop + ViewportIndicator.Height;

            if (pos.Y >= viewportTop && pos.Y <= viewportBottom)
            {
                // Start dragging
                _isDragging = true;
                _dragStartY = pos.Y;
                _dragStartScrollOffset = _editor.TextArea.TextView.VerticalOffset;
                MinimapCanvas.CaptureMouse();
            }
            else
            {
                // Click to scroll to position
                ScrollToMinimapPosition(pos.Y);
            }

            e.Handled = true;
        }

        private void MinimapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                MinimapCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MinimapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _editor != null)
            {
                var pos = e.GetPosition(MinimapCanvas);
                var deltaY = pos.Y - _dragStartY;

                // Convert minimap delta to editor scroll delta
                var editorLineHeight = _editor.TextArea.TextView.DefaultLineHeight;
                var scrollDelta = (deltaY / LineHeight) * editorLineHeight;

                var newOffset = _dragStartScrollOffset + scrollDelta;
                newOffset = Math.Max(0, newOffset);

                _editor.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
        }

        private void MinimapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Forward mouse wheel to editor for scrolling
            if (_editor != null)
            {
                var newOffset = _editor.TextArea.TextView.VerticalOffset - (e.Delta / 3.0);
                newOffset = Math.Max(0, newOffset);
                _editor.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
        }

        private void ScrollToMinimapPosition(double minimapY)
        {
            if (_editor == null) return;

            try
            {
                // Calculate target line from minimap Y position
                var targetLine = (int)(minimapY / LineHeight) + 1;
                targetLine = Math.Max(1, Math.Min(targetLine, _editor.Document.LineCount));

                // Calculate offset to center the target line in the viewport
                var editorLineHeight = _editor.TextArea.TextView.DefaultLineHeight;
                var viewportHeightInLines = _editor.TextArea.TextView.ActualHeight / editorLineHeight;

                var targetOffset = (targetLine - viewportHeightInLines / 2) * editorLineHeight;
                targetOffset = Math.Max(0, targetOffset);

                _editor.ScrollToVerticalOffset(targetOffset);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scroll error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class to host a DrawingVisual in a Canvas.
    /// </summary>
    internal class VisualHost : FrameworkElement
    {
        private readonly Visual _visual;

        public VisualHost(Visual visual)
        {
            _visual = visual;
            AddVisualChild(visual);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _visual;
        }
    }
}
