using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfShapes = System.Windows.Shapes;
using WpfCanvas = System.Windows.Controls.Canvas;
using Code2Viz.Animation;
using Code2Viz.Geometry;

namespace Code2Viz;

public partial class TimelinePanel : UserControl
{
    private Timeline? _timeline;
    private double _pixelsPerSecond = 50;
    private const double TrackHeight = 24;
    private const double TrackSpacing = 2;
    private bool _isDraggingPlayhead = false;

    public event EventHandler<double>? TimeChanged;

    public TimelinePanel()
    {
        InitializeComponent();
    }

    public void SetTimeline(Timeline? timeline)
    {
        _timeline = timeline;

        if (_timeline == null)
        {
            EmptyStateOverlay.Visibility = Visibility.Visible;
            ClearTracks();
            return;
        }

        EmptyStateOverlay.Visibility = Visibility.Collapsed;
        RefreshTimeline();
    }

    public void RefreshTimeline()
    {
        if (_timeline == null) return;

        DrawTimeRuler();
        DrawTracks();
        UpdatePlayhead();
        TimeInfoText.Text = $"Duration: {_timeline.Duration:F2}s";
    }

    public void UpdatePlayhead()
    {
        if (_timeline == null) return;

        var x = _timeline.CurrentTime * _pixelsPerSecond;
        WpfCanvas.SetLeft(PlayheadLine, x);
        WpfCanvas.SetLeft(PlayheadTriangle, x - 4);
    }

    private void ClearTracks()
    {
        TracksCanvas.Children.Clear();
        TimeRulerCanvas.Children.Clear();
    }

    private void DrawTimeRuler()
    {
        TimeRulerCanvas.Children.Clear();

        if (_timeline == null) return;

        var duration = _timeline.Duration;
        var width = duration * _pixelsPerSecond + 20; // Small padding at end
        TimeRulerCanvas.Width = width;

        // Determine tick interval based on zoom level
        double majorTickInterval;
        if (_pixelsPerSecond >= 100) majorTickInterval = 1.0;
        else if (_pixelsPerSecond >= 50) majorTickInterval = 2.0;
        else if (_pixelsPerSecond >= 20) majorTickInterval = 5.0;
        else majorTickInterval = 10.0;

        double minorTickInterval = majorTickInterval / 5;

        // Draw ticks
        for (double t = 0; t <= duration + 0.001; t += minorTickInterval)
        {
            var x = t * _pixelsPerSecond;
            bool isMajor = Math.Abs(t % majorTickInterval) < 0.001 || Math.Abs(t % majorTickInterval - majorTickInterval) < 0.001;

            var line = new WpfShapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = isMajor ? 0 : 12,
                Y2 = 24,
                Stroke = isMajor ? Brushes.Gray : Brushes.DarkGray,
                StrokeThickness = isMajor ? 1 : 0.5
            };
            TimeRulerCanvas.Children.Add(line);

            if (isMajor)
            {
                var text = new TextBlock
                {
                    Text = $"{t:F1}s",
                    Foreground = Brushes.Gray,
                    FontSize = 10
                };
                WpfCanvas.SetLeft(text, x + 2);
                WpfCanvas.SetTop(text, 2);
                TimeRulerCanvas.Children.Add(text);
            }
        }
    }

    private void DrawTracks()
    {
        TracksCanvas.Children.Clear();

        if (_timeline == null) return;

        var duration = _timeline.Duration;
        var width = duration * _pixelsPerSecond + 20; // Small padding at end
        var animations = _timeline.Animations;

        var totalHeight = animations.Count * (TrackHeight + TrackSpacing) + TrackSpacing;
        TracksCanvas.Width = width;
        TracksCanvas.Height = totalHeight;

        // One track per animation
        for (int i = 0; i < animations.Count; i++)
        {
            var anim = animations[i];
            var y = i * (TrackHeight + TrackSpacing) + TrackSpacing;

            // Track background
            var trackBg = new WpfShapes.Rectangle
            {
                Width = width,
                Height = TrackHeight,
                Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40))
            };
            WpfCanvas.SetTop(trackBg, y);
            TracksCanvas.Children.Add(trackBg);

            // Animation bar
            var x = anim.StartTime * _pixelsPerSecond;
            var barWidth = anim.Duration * _pixelsPerSecond;

            var color = GetAnimationColor(anim);

            var bar = new WpfShapes.Rectangle
            {
                Width = Math.Max(barWidth, 4),
                Height = TrackHeight - 4,
                Fill = new SolidColorBrush(color),
                RadiusX = 3,
                RadiusY = 3,
                Cursor = Cursors.Hand,
                ToolTip = $"{anim.GetType().Name}\nStart: {anim.StartTime:F2}s\nDuration: {anim.Duration:F2}s"
            };
            WpfCanvas.SetLeft(bar, x);
            WpfCanvas.SetTop(bar, y + 2);
            TracksCanvas.Children.Add(bar);

            // Animation name label - centered in bar
            var barHeight = TrackHeight - 4;
            var label = new TextBlock
            {
                Text = GetAnimationLabel(anim),
                Foreground = Brushes.White,
                FontSize = 10,
                IsHitTestVisible = false,
                Width = Math.Max(barWidth, 4),
                TextAlignment = TextAlignment.Center
            };
            WpfCanvas.SetLeft(label, x);
            // Vertically center: y + 2 (bar top) + (barHeight - fontSize) / 2
            WpfCanvas.SetTop(label, y + 2 + (barHeight - 10) / 2);
            TracksCanvas.Children.Add(label);
        }
    }

    private Color GetAnimationColor(Animation.Animation anim)
    {
        return anim switch
        {
            DrawAnimation => Color.FromRgb(76, 175, 80),      // Green
            MoveAnimation => Color.FromRgb(33, 150, 243),     // Blue
            RotateAnimation => Color.FromRgb(156, 39, 176),   // Purple
            FlipAnimation => Color.FromRgb(255, 152, 0),      // Orange
            FadeInAnimation => Color.FromRgb(96, 125, 139),   // Blue Gray
            FadeOutAnimation => Color.FromRgb(233, 30, 99),   // Pink
            _ => Color.FromRgb(158, 158, 158)                 // Gray
        };
    }

    private string GetAnimationLabel(Animation.Animation anim)
    {
        // Use the animation's Name property if set (e.g., variable name from code)
        if (!string.IsNullOrEmpty(anim.Name))
            return anim.Name;

        // Fall back to type name
        return anim switch
        {
            DrawAnimation => "Draw",
            MoveAnimation => "Move",
            RotateAnimation => "Rotate",
            FlipAnimation => "Flip",
            FadeInAnimation => "FadeIn",
            FadeOutAnimation => "FadeOut",
            _ => anim.GetType().Name.Replace("Animation", "")
        };
    }

    private void TracksCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_timeline == null) return;

        _isDraggingPlayhead = true;
        TracksCanvas.CaptureMouse();

        var pos = e.GetPosition(TracksCanvas);
        var time = pos.X / _pixelsPerSecond;
        time = Math.Max(0, Math.Min(time, _timeline.Duration));

        if (SnapCheckBox.IsChecked == true)
        {
            time = SnapToGrid(time);
        }

        _timeline.Update(time);
        UpdatePlayhead();
        TimeChanged?.Invoke(this, time);
    }

    private void TracksCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPlayhead || _timeline == null) return;

        var pos = e.GetPosition(TracksCanvas);
        var time = pos.X / _pixelsPerSecond;
        time = Math.Max(0, Math.Min(time, _timeline.Duration));

        if (SnapCheckBox.IsChecked == true)
        {
            time = SnapToGrid(time);
        }

        _timeline.Update(time);
        UpdatePlayhead();
        TimeChanged?.Invoke(this, time);
    }

    private void TracksCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingPlayhead)
        {
            _isDraggingPlayhead = false;
            TracksCanvas.ReleaseMouseCapture();
        }
    }

    private double SnapToGrid(double time)
    {
        double gridSize;
        if (_pixelsPerSecond >= 100) gridSize = 0.1;
        else if (_pixelsPerSecond >= 50) gridSize = 0.25;
        else if (_pixelsPerSecond >= 20) gridSize = 0.5;
        else gridSize = 1.0;

        return Math.Round(time / gridSize) * gridSize;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomSlider == null || ZoomText == null) return;

        _pixelsPerSecond = ZoomSlider.Value;
        ZoomText.Text = $"{_pixelsPerSecond:F0} px/s";
        RefreshTimeline();
    }

    private void TracksScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync time ruler with tracks scroll (horizontal) using RenderTransform
        TimeRulerCanvas.RenderTransform = new TranslateTransform(-TracksScroll.HorizontalOffset, 0);
    }
}
