using System.Windows;
using Code2Viz.Geometry;

namespace Code2Viz.Canvas;

/// <summary>
/// Event args for control point drag ended event.
/// </summary>
public class ControlPointDragEndedEventArgs : EventArgs
{
    public Shape Shape { get; }
    public int ControlPointIndex { get; }

    public ControlPointDragEndedEventArgs(Shape shape, int controlPointIndex)
    {
        Shape = shape;
        ControlPointIndex = controlPointIndex;
    }
}

/// <summary>
/// Manages shape selection state and interaction.
/// </summary>
public class SelectionTool
{
    private const double HitTolerance = 8.0; // Screen pixels
    private const double HandleTolerance = 10.0; // Screen pixels for handles

    /// <summary>
    /// The snap engine used for detecting snap points while dragging.
    /// </summary>
    public SnapEngine SnapEngine { get; }

    /// <summary>
    /// Current snap result during dragging (if any).
    /// </summary>
    public SnapResult? CurrentSnap { get; private set; }

    /// <summary>
    /// Currently selected shapes.
    /// </summary>
    public List<Shape> SelectedShapes { get; } = new();

    public SelectionTool()
    {
        SnapEngine = new SnapEngine();
        SnapEngine.SyncFromSettings();
    }

    /// <summary>
    /// Whether a box selection is in progress.
    /// </summary>
    public bool IsBoxSelecting { get; private set; }

    /// <summary>
    /// Start point of selection box in world coordinates.
    /// </summary>
    public VPoint? BoxStart { get; private set; }

    /// <summary>
    /// Current end point of selection box in world coordinates.
    /// </summary>
    public VPoint? BoxEnd { get; private set; }

    /// <summary>
    /// Whether a control point is being dragged.
    /// </summary>
    public bool IsDraggingHandle { get; private set; }

    /// <summary>
    /// The shape whose control point is being dragged.
    /// </summary>
    public Shape? DraggedShape { get; private set; }

    /// <summary>
    /// Index of the control point being dragged.
    /// </summary>
    public int DraggedControlPointIndex { get; private set; } = -1;

    /// <summary>
    /// Current position of the dragged control point.
    /// </summary>
    public VPoint? DragPosition { get; private set; }

    /// <summary>
    /// Event raised when selection changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Event raised when a control point is moved.
    /// </summary>
    public event EventHandler? ControlPointMoved;

    /// <summary>
    /// Event raised when a control point drag operation ends.
    /// </summary>
    public event EventHandler<ControlPointDragEndedEventArgs>? ControlPointDragEnded;

    /// <summary>
    /// Performs hit testing to find a shape at the given position.
    /// </summary>
    /// <param name="worldPos">Position in world coordinates.</param>
    /// <param name="shapes">Shapes to test against.</param>
    /// <param name="scale">Current canvas scale for tolerance calculation.</param>
    /// <returns>The topmost shape at the position, or null if none.</returns>
    public Shape? HitTest(VPoint worldPos, IReadOnlyList<IDrawable> shapes, double scale)
    {
        var tolerance = HitTolerance / scale;

        // Iterate in reverse to find topmost shape first
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            if (shapes[i] is Shape shape)
            {
                if (HitTestShape(shape, worldPos, tolerance))
                    return shape;
            }
        }

        return null;
    }

    /// <summary>
    /// Tests if a point hits a specific shape.
    /// </summary>
    private bool HitTestShape(Shape shape, VPoint point, double tolerance)
    {
        return shape switch
        {
            VPoint p => DistanceToPoint(p, point) <= tolerance,
            VLine line => DistanceToLine(line, point) <= tolerance,
            VCircle circle => DistanceToCircle(circle, point) <= tolerance,
            VArc arc => DistanceToArc(arc, point) <= tolerance,
            VRectangle rect => HitTestRectangle(rect, point, tolerance),
            VEllipse ellipse => HitTestEllipse(ellipse, point, tolerance),
            VPolygon polygon => HitTestPolygon(polygon, point, tolerance),
            VPolyline polyline => DistanceToPolyline(polyline, point) <= tolerance,
            VBezier bezier => DistanceToBezier(bezier, point) <= tolerance,
            VSpline spline => DistanceToSpline(spline, point) <= tolerance,
            VArrow arrow => DistanceToLine(new VLine(arrow.Start, arrow.End), point) <= tolerance,
            VText text => HitTestText(text, point, tolerance),
            VDimension dim => HitTestDimension(dim, point, tolerance),
            VGroup group => HitTestGroup(group, point, tolerance),
            _ => IsWithinBoundingBox(shape, point, tolerance)
        };
    }

    private double DistanceToPoint(VPoint p, VPoint testPoint)
    {
        return p.DistanceTo(testPoint);
    }

    private double DistanceToLine(VLine line, VPoint point)
    {
        var dx = line.End.X - line.Start.X;
        var dy = line.End.Y - line.Start.Y;
        var lengthSq = dx * dx + dy * dy;

        if (lengthSq < 0.0001)
            return point.DistanceTo(line.Start);

        var t = Math.Clamp(((point.X - line.Start.X) * dx + (point.Y - line.Start.Y) * dy) / lengthSq, 0, 1);
        var projX = line.Start.X + t * dx;
        var projY = line.Start.Y + t * dy;

        return Math.Sqrt((point.X - projX) * (point.X - projX) + (point.Y - projY) * (point.Y - projY));
    }

    private double DistanceToCircle(VCircle circle, VPoint point)
    {
        var distToCenter = point.DistanceTo(circle.Center);

        // Check if filled
        if (circle.FillColor != "Transparent" && distToCenter <= circle.Radius)
            return 0;

        // Distance to the circle stroke
        return Math.Abs(distToCenter - circle.Radius);
    }

    private double DistanceToArc(VArc arc, VPoint point)
    {
        var distToCenter = point.DistanceTo(arc.Center);
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X) * 180 / Math.PI;

        // Normalize angle to check if within arc
        var start = arc.StartAngle;
        var end = arc.EndAngle;

        // Normalize angles
        while (angle < 0) angle += 360;
        while (start < 0) start += 360;
        while (end < 0) end += 360;

        bool isWithinArc;
        if (start <= end)
            isWithinArc = angle >= start && angle <= end;
        else
            isWithinArc = angle >= start || angle <= end;

        if (!isWithinArc)
            return double.MaxValue;

        return Math.Abs(distToCenter - arc.Radius);
    }

    private bool HitTestRectangle(VRectangle rect, VPoint point, double tolerance)
    {
        // Check if inside (for filled rectangles)
        if (rect.FillColor != "Transparent")
        {
            if (point.X >= rect.Corner.X && point.X <= rect.Corner.X + rect.Width &&
                point.Y >= rect.Corner.Y && point.Y <= rect.Corner.Y + rect.Height)
                return true;
        }

        // Check distance to edges
        var lines = new[]
        {
            new VLine(rect.Corner, new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y)),
            new VLine(new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y), new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height)),
            new VLine(new VPoint(rect.Corner.X + rect.Width, rect.Corner.Y + rect.Height), new VPoint(rect.Corner.X, rect.Corner.Y + rect.Height)),
            new VLine(new VPoint(rect.Corner.X, rect.Corner.Y + rect.Height), rect.Corner)
        };

        return lines.Any(line => DistanceToLine(line, point) <= tolerance);
    }

    private bool HitTestEllipse(VEllipse ellipse, VPoint point, double tolerance)
    {
        // Normalized point relative to ellipse center
        var nx = (point.X - ellipse.Center.X) / ellipse.RadiusX;
        var ny = (point.Y - ellipse.Center.Y) / ellipse.RadiusY;
        var normalizedDist = Math.Sqrt(nx * nx + ny * ny);

        // Check if filled
        if (ellipse.FillColor != "Transparent" && normalizedDist <= 1.0)
            return true;

        // Check stroke (approximate tolerance)
        var avgRadius = (ellipse.RadiusX + ellipse.RadiusY) / 2;
        return Math.Abs(normalizedDist - 1.0) * avgRadius <= tolerance;
    }

    private bool HitTestPolygon(VPolygon polygon, VPoint point, double tolerance)
    {
        if (polygon.Points.Count < 3)
            return false;

        // Check if inside (for filled polygons)
        if (polygon.FillColor != "Transparent" && IsPointInPolygon(polygon.Points, point))
            return true;

        // Check distance to edges
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            var j = (i + 1) % polygon.Points.Count;
            var line = new VLine(polygon.Points[i], polygon.Points[j]);
            if (DistanceToLine(line, point) <= tolerance)
                return true;
        }

        return false;
    }

    private bool IsPointInPolygon(IReadOnlyList<VPoint> polygon, VPoint point)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    private double DistanceToPolyline(VPolyline polyline, VPoint point)
    {
        if (polyline.Points.Count < 2)
            return double.MaxValue;

        double minDist = double.MaxValue;
        for (int i = 0; i < polyline.Points.Count - 1; i++)
        {
            var line = new VLine(polyline.Points[i], polyline.Points[i + 1]);
            minDist = Math.Min(minDist, DistanceToLine(line, point));
        }

        return minDist;
    }

    private double DistanceToBezier(VBezier bezier, VPoint point)
    {
        var renderPoints = bezier.GetRenderPoints();
        if (renderPoints.Count < 2)
            return double.MaxValue;

        double minDist = double.MaxValue;
        for (int i = 0; i < renderPoints.Count - 1; i++)
        {
            var line = new VLine(renderPoints[i], renderPoints[i + 1]);
            minDist = Math.Min(minDist, DistanceToLine(line, point));
        }

        return minDist;
    }

    private double DistanceToSpline(VSpline spline, VPoint point)
    {
        var renderPoints = spline.GetRenderPoints();
        if (renderPoints.Count < 2)
            return double.MaxValue;

        double minDist = double.MaxValue;
        for (int i = 0; i < renderPoints.Count - 1; i++)
        {
            var line = new VLine(renderPoints[i], renderPoints[i + 1]);
            minDist = Math.Min(minDist, DistanceToLine(line, point));
        }

        return minDist;
    }

    private bool HitTestText(VText text, VPoint point, double tolerance)
    {
        // Approximate text bounding box
        var estimatedWidth = text.Content.Length * text.Height * 0.6;
        return point.X >= text.Location.X - tolerance &&
               point.X <= text.Location.X + estimatedWidth + tolerance &&
               point.Y >= text.Location.Y - tolerance &&
               point.Y <= text.Location.Y + text.Height + tolerance;
    }

    private bool HitTestDimension(VDimension dim, VPoint point, double tolerance)
    {
        var (dimStart, dimEnd, _, ext1Start, ext1End, ext2Start, ext2End) = dim.GetDimensionGeometry();

        // Check dimension line
        if (DistanceToLine(new VLine(dimStart, dimEnd), point) <= tolerance)
            return true;

        // Check extension lines
        if (DistanceToLine(new VLine(ext1Start, ext1End), point) <= tolerance)
            return true;
        if (DistanceToLine(new VLine(ext2Start, ext2End), point) <= tolerance)
            return true;

        return false;
    }

    private bool HitTestGroup(VGroup group, VPoint point, double tolerance)
    {
        foreach (var shape in group.Shapes)
        {
            if (HitTestShape(shape, point, tolerance))
                return true;
        }
        return false;
    }

    private bool IsWithinBoundingBox(Shape shape, VPoint point, double tolerance)
    {
        var (min, max) = shape.GetBounds();
        return point.X >= min.X - tolerance && point.X <= max.X + tolerance &&
               point.Y >= min.Y - tolerance && point.Y <= max.Y + tolerance;
    }

    /// <summary>
    /// Hit tests control points for selected shapes.
    /// </summary>
    /// <param name="worldPos">Position in world coordinates.</param>
    /// <param name="scale">Current canvas scale.</param>
    /// <returns>Tuple of (shape, control point index) or (null, -1) if no hit.</returns>
    public (Shape? shape, int index) HitTestControlPoints(VPoint worldPos, double scale)
    {
        var tolerance = HandleTolerance / scale;

        foreach (var shape in SelectedShapes)
        {
            var controlPoints = shape.GetControlPoints();
            for (int i = 0; i < controlPoints.Count; i++)
            {
                var cp = controlPoints[i];
                var dist = Math.Sqrt((worldPos.X - cp.X) * (worldPos.X - cp.X) + (worldPos.Y - cp.Y) * (worldPos.Y - cp.Y));
                if (dist <= tolerance)
                {
                    return (shape, i);
                }
            }
        }

        return (null, -1);
    }

    /// <summary>
    /// Handles mouse down for selection.
    /// </summary>
    /// <param name="worldPos">Position in world coordinates.</param>
    /// <param name="shift">Whether Shift key is pressed (add to selection).</param>
    /// <param name="ctrl">Whether Ctrl key is pressed (toggle selection).</param>
    /// <param name="shapes">All shapes on the canvas.</param>
    /// <param name="scale">Current canvas scale.</param>
    /// <returns>True if a shape was clicked, false to start box selection.</returns>
    public bool OnMouseDown(VPoint worldPos, bool shift, bool ctrl, IReadOnlyList<IDrawable> shapes, double scale)
    {
        // First, check if we're clicking on a control point of a selected shape
        if (SelectedShapes.Count > 0)
        {
            var (hitShape, hitIndex) = HitTestControlPoints(worldPos, scale);
            if (hitShape != null && hitIndex >= 0)
            {
                // Start dragging the control point
                IsDraggingHandle = true;
                DraggedShape = hitShape;
                DraggedControlPointIndex = hitIndex;
                DragPosition = worldPos;
                return true;
            }
        }

        var hitShapeNormal = HitTest(worldPos, shapes, scale);

        if (hitShapeNormal != null)
        {
            if (ctrl)
            {
                // Toggle selection
                if (SelectedShapes.Contains(hitShapeNormal))
                    SelectedShapes.Remove(hitShapeNormal);
                else
                    SelectedShapes.Add(hitShapeNormal);
            }
            else if (shift)
            {
                // Add to selection
                if (!SelectedShapes.Contains(hitShapeNormal))
                    SelectedShapes.Add(hitShapeNormal);
            }
            else
            {
                // Replace selection (unless already selected for potential drag)
                if (!SelectedShapes.Contains(hitShapeNormal))
                {
                    SelectedShapes.Clear();
                    SelectedShapes.Add(hitShapeNormal);
                }
            }

            UpdateSelectionState();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        else
        {
            // Start box selection
            if (!shift && !ctrl)
            {
                SelectedShapes.Clear();
                UpdateSelectionState();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            IsBoxSelecting = true;
            BoxStart = worldPos;
            BoxEnd = worldPos;
            return false;
        }
    }

    /// <summary>
    /// Handles mouse move during box selection or handle dragging.
    /// </summary>
    /// <param name="worldPos">Current mouse position in world coordinates.</param>
    /// <param name="shapes">All shapes on the canvas (for snapping).</param>
    /// <param name="scale">Current canvas scale (for snap tolerance calculation).</param>
    public void OnMouseMove(VPoint worldPos, IReadOnlyList<IDrawable>? shapes = null, double scale = 1.0)
    {
        if (IsDraggingHandle && DraggedShape != null)
        {
            // Try to snap to other shapes (exclude the shape being dragged)
            VPoint targetPos = worldPos;
            CurrentSnap = null;

            if (shapes != null)
            {
                // Filter out the dragged shape from snap targets
                var snapTargets = shapes.Where(s => s != DraggedShape).ToList();
                CurrentSnap = SnapEngine.FindSnapPoint(worldPos, snapTargets, scale);

                if (CurrentSnap != null)
                {
                    targetPos = CurrentSnap.Point;
                }
            }

            DragPosition = targetPos;
            DraggedShape.MoveControlPoint(DraggedControlPointIndex, targetPos);
            ControlPointMoved?.Invoke(this, EventArgs.Empty);
        }
        else if (IsBoxSelecting)
        {
            BoxEnd = worldPos;
            CurrentSnap = null;
        }
        else
        {
            CurrentSnap = null;
        }
    }

    /// <summary>
    /// Refreshes snap settings from application settings.
    /// </summary>
    public void RefreshSnapSettings()
    {
        SnapEngine.SyncFromSettings();
    }

    /// <summary>
    /// Handles mouse up to complete box selection or handle dragging.
    /// </summary>
    /// <param name="worldPos">Position in world coordinates.</param>
    /// <param name="shapes">All shapes on the canvas.</param>
    /// <param name="shift">Whether Shift key is pressed.</param>
    /// <param name="ctrl">Whether Ctrl key is pressed.</param>
    public void OnMouseUp(VPoint worldPos, IReadOnlyList<IDrawable> shapes, bool shift, bool ctrl)
    {
        // End handle dragging
        if (IsDraggingHandle)
        {
            // Fire event before clearing the shape reference
            if (DraggedShape != null)
            {
                ControlPointDragEnded?.Invoke(this, new ControlPointDragEndedEventArgs(DraggedShape, DraggedControlPointIndex));
            }

            IsDraggingHandle = false;
            DraggedShape = null;
            DraggedControlPointIndex = -1;
            DragPosition = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty); // Trigger update to refresh properties panel
            return;
        }

        if (IsBoxSelecting && BoxStart != null)
        {
            BoxEnd = worldPos;

            var minX = Math.Min(BoxStart.X, BoxEnd.X);
            var maxX = Math.Max(BoxStart.X, BoxEnd.X);
            var minY = Math.Min(BoxStart.Y, BoxEnd.Y);
            var maxY = Math.Max(BoxStart.Y, BoxEnd.Y);

            // Only select if box has some size
            if (maxX - minX > 0.1 || maxY - minY > 0.1)
            {
                var boxSelectedShapes = new List<Shape>();

                foreach (var drawable in shapes)
                {
                    if (drawable is Shape shape)
                    {
                        var (boundsMin, boundsMax) = shape.GetBounds();

                        // Check if shape bounds intersect with selection box
                        if (boundsMax.X >= minX && boundsMin.X <= maxX &&
                            boundsMax.Y >= minY && boundsMin.Y <= maxY)
                        {
                            boxSelectedShapes.Add(shape);
                        }
                    }
                }

                if (shift || ctrl)
                {
                    // Add to existing selection
                    foreach (var shape in boxSelectedShapes)
                    {
                        if (!SelectedShapes.Contains(shape))
                            SelectedShapes.Add(shape);
                    }
                }
                else
                {
                    // Replace selection
                    SelectedShapes.Clear();
                    SelectedShapes.AddRange(boxSelectedShapes);
                }

                UpdateSelectionState();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        IsBoxSelecting = false;
        BoxStart = null;
        BoxEnd = null;
    }

    /// <summary>
    /// Selects all shapes.
    /// </summary>
    public void SelectAll(IReadOnlyList<IDrawable> shapes)
    {
        SelectedShapes.Clear();
        foreach (var drawable in shapes)
        {
            if (drawable is Shape shape)
                SelectedShapes.Add(shape);
        }
        UpdateSelectionState();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    public void ClearSelection()
    {
        if (SelectedShapes.Count > 0)
        {
            foreach (var shape in SelectedShapes)
            {
                shape.IsSelected = false;
            }
            SelectedShapes.Clear();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Notifies listeners that the selection has changed.
    /// Call this after manually modifying SelectedShapes.
    /// </summary>
    public void NotifySelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the IsSelected property on all shapes.
    /// </summary>
    private void UpdateSelectionState()
    {
        // This would require access to all shapes - for now, we track in SelectedShapes list
        foreach (var shape in SelectedShapes)
        {
            shape.IsSelected = true;
        }
    }

    /// <summary>
    /// Removes a shape from the selection if present.
    /// </summary>
    public void RemoveFromSelection(Shape shape)
    {
        if (SelectedShapes.Remove(shape))
        {
            shape.IsSelected = false;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
