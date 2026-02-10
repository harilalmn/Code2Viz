using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;


namespace Code2Viz.Editor
{
    public class VizTextMarkerService : IBackgroundRenderer, ITextViewConnect
    {
        private readonly TextDocument _document;
        private readonly TextSegmentCollection<VizTextMarker> _markers;

        /// <summary>
        /// Raised when markers are added, removed, or cleared.
        /// </summary>
        public event EventHandler? MarkersChanged;

        public VizTextMarkerService(TextDocument document)
        {
            _document = document;
            _markers = new TextSegmentCollection<VizTextMarker>(document);
        }

        /// <summary>
        /// Gets all current markers.
        /// </summary>
        public IEnumerable<VizTextMarker> GetMarkers() => _markers.ToList();

        public VizTextMarker Create(int offset, int length, string message, Color color)
        {
            var m = new VizTextMarker(offset, length);
            _markers.Add(m);
            m.Message = message;
            m.MarkerColor = color;
            m.MarkerType = VizTextMarkerType.SquigglyUnderline;
            Redraw(m);
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            return m;
        }

        public void RemoveAll(Predicate<VizTextMarker> predicate)
        {
            if (predicate == null) return;
            foreach (var m in _markers.ToArray())
            {
                if (predicate(m))
                {
                    Remove(m);
                }
            }
        }

        public void Remove(VizTextMarker marker)
        {
            if (_markers.Remove(marker))
            {
                Redraw(marker);
            }
        }

        public void Clear()
        {
            foreach (var m in _markers)
            {
                Redraw(m);
            }
            _markers.Clear();
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        public VizTextMarker? GetMarkerAtOffset(int offset)
        {
            return _markers.FindSegmentsContaining(offset).FirstOrDefault();
        }

        private void Redraw(ISegment segment)
        {
            foreach (var view in _textViews)
            {
                view.Redraw(segment);
            }
        }

        private readonly List<TextView> _textViews = new List<TextView>();

        void ITextViewConnect.AddToTextView(TextView textView)
        {
            if (textView != null && !_textViews.Contains(textView))
            {
                _textViews.Add(textView);
            }
        }

        void ITextViewConnect.RemoveFromTextView(TextView textView)
        {
            if (textView != null)
            {
                _textViews.Remove(textView);
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;

            if (_markers == null || !_textViews.Contains(textView)) return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0) return;

            int viewStart = visualLines.First().FirstDocumentLine.Offset;
            int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            foreach (var marker in _markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
            {
                if (marker.MarkerColor.HasValue)
                {
                    foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                    {
                        var startPoint = r.BottomLeft;
                        var endPoint = r.BottomRight;

                        var usedPen = new Pen(new SolidColorBrush(marker.MarkerColor.Value), 1);
                        usedPen.Freeze();

                        // Create squiggly line geometry
                        var period = 3;
                        var amplitude = 1;

                        StreamGeometry geometry = new StreamGeometry();
                        using (StreamGeometryContext ctx = geometry.Open())
                        {
                            ctx.BeginFigure(startPoint, false, false);
                            double x = startPoint.X;
                            double y = startPoint.Y;
                            bool down = false;

                            while (x < endPoint.X)
                            {
                                x += period;
                                y = down ? startPoint.Y : startPoint.Y + amplitude;
                                down = !down;
                                if (x > endPoint.X) x = endPoint.X;
                                ctx.LineTo(new Point(x, y), true, true);
                            }
                        }
                        geometry.Freeze();
                        drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
                    }
                }
            }
        }
    }

    public class VizTextMarker : TextSegment
    {
        public VizTextMarker(int startOffset, int length)
        {
            StartOffset = startOffset;
            Length = length;
        }

        public string? Message { get; set; }
        public Color? MarkerColor { get; set; }
        public VizTextMarkerType MarkerType { get; set; }
    }

    public enum VizTextMarkerType
    {
        SquigglyUnderline
    }
}
