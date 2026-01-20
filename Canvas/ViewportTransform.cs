using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Code2Viz.Geometry;

namespace Code2Viz.Canvas
{
    /// <summary>
    /// Encapsulates viewport transformation logic for converting between world and screen coordinates.
    /// Handles zoom, pan, and viewport fitting operations for CAD-style canvas rendering.
    ///
    /// Coordinate System:
    /// - World: Mathematical Y-up coordinate system with origin at canvas center
    /// - Screen: WPF pixel coordinates with Y-down, origin at top-left
    /// </summary>
    public class ViewportTransform
    {
        // Zoom constraints
        public const double MinZoom = 0.01;
        public const double MaxZoom = 100.0;
        public const double DefaultZoomFactor = 1.1;

        private double _scale = 1.0;
        private double _panX = 0;
        private double _panY = 0;
        private double _viewportWidth;
        private double _viewportHeight;

        /// <summary>
        /// Current zoom scale. 1.0 = 100%, 2.0 = 200%, etc.
        /// </summary>
        public double Scale
        {
            get => _scale;
            set => _scale = Math.Clamp(value, MinZoom, MaxZoom);
        }

        /// <summary>
        /// Horizontal pan offset in screen pixels.
        /// </summary>
        public double PanX
        {
            get => _panX;
            set => _panX = value;
        }

        /// <summary>
        /// Vertical pan offset in screen pixels.
        /// </summary>
        public double PanY
        {
            get => _panY;
            set => _panY = value;
        }

        /// <summary>
        /// Viewport width in screen pixels.
        /// </summary>
        public double ViewportWidth
        {
            get => _viewportWidth;
            set => _viewportWidth = Math.Max(0, value);
        }

        /// <summary>
        /// Viewport height in screen pixels.
        /// </summary>
        public double ViewportHeight
        {
            get => _viewportHeight;
            set => _viewportHeight = Math.Max(0, value);
        }

        /// <summary>
        /// Gets the center of the viewport in screen coordinates.
        /// </summary>
        public Point ViewportCenter => new Point(_viewportWidth / 2, _viewportHeight / 2);

        /// <summary>
        /// Gets the current world coordinate at the viewport center.
        /// </summary>
        public Point WorldCenter => ScreenToWorld(ViewportCenter);

        /// <summary>
        /// Event raised when the transform changes (zoom, pan, or viewport resize).
        /// </summary>
        public event EventHandler? TransformChanged;

        /// <summary>
        /// Creates a new viewport transform with default settings.
        /// </summary>
        public ViewportTransform()
        {
        }

        /// <summary>
        /// Creates a viewport transform with specified viewport dimensions.
        /// </summary>
        public ViewportTransform(double viewportWidth, double viewportHeight)
        {
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
        }

        /// <summary>
        /// Updates the viewport dimensions. Call when canvas is resized.
        /// </summary>
        public void SetViewportSize(double width, double height)
        {
            _viewportWidth = Math.Max(0, width);
            _viewportHeight = Math.Max(0, height);
            OnTransformChanged();
        }

        /// <summary>
        /// Converts world coordinates to screen coordinates.
        /// World: Y-up, origin at canvas center
        /// Screen: Y-down, origin at top-left
        /// </summary>
        public Point WorldToScreen(double worldX, double worldY)
        {
            double screenX = _viewportWidth / 2 + (worldX * _scale) + _panX;
            double screenY = _viewportHeight / 2 - (worldY * _scale) + _panY;
            return new Point(screenX, screenY);
        }

        /// <summary>
        /// Converts world coordinates to screen coordinates.
        /// </summary>
        public Point WorldToScreen(Point worldPoint)
            => WorldToScreen(worldPoint.X, worldPoint.Y);

        /// <summary>
        /// Converts a VPoint to screen coordinates.
        /// </summary>
        public Point WorldToScreen(VPoint worldPoint)
            => WorldToScreen(worldPoint.X, worldPoint.Y);

        /// <summary>
        /// Converts screen coordinates to world coordinates.
        /// </summary>
        public Point ScreenToWorld(double screenX, double screenY)
        {
            double worldX = (screenX - _viewportWidth / 2 - _panX) / _scale;
            double worldY = -(screenY - _viewportHeight / 2 - _panY) / _scale;
            return new Point(worldX, worldY);
        }

        /// <summary>
        /// Converts screen coordinates to world coordinates.
        /// </summary>
        public Point ScreenToWorld(Point screenPoint)
            => ScreenToWorld(screenPoint.X, screenPoint.Y);

        /// <summary>
        /// Converts a world-space distance to screen pixels.
        /// </summary>
        public double WorldToScreenDistance(double worldDistance)
            => worldDistance * _scale;

        /// <summary>
        /// Converts a screen pixel distance to world units.
        /// </summary>
        public double ScreenToWorldDistance(double screenDistance)
            => screenDistance / _scale;

        /// <summary>
        /// Converts a world-space rectangle to screen coordinates.
        /// Note: Y values are flipped due to coordinate system difference.
        /// </summary>
        public Rect WorldToScreenRect(double minX, double minY, double maxX, double maxY)
        {
            var topLeft = WorldToScreen(minX, maxY);     // World maxY = screen top
            var bottomRight = WorldToScreen(maxX, minY); // World minY = screen bottom
            return new Rect(topLeft, bottomRight);
        }

        /// <summary>
        /// Gets the visible world bounds (the portion of world space visible in the viewport).
        /// </summary>
        public Rect GetVisibleWorldBounds()
        {
            var topLeft = ScreenToWorld(0, 0);
            var bottomRight = ScreenToWorld(_viewportWidth, _viewportHeight);
            return new Rect(
                Math.Min(topLeft.X, bottomRight.X),
                Math.Min(topLeft.Y, bottomRight.Y),
                Math.Abs(bottomRight.X - topLeft.X),
                Math.Abs(bottomRight.Y - topLeft.Y));
        }

        /// <summary>
        /// Gets the visible world bounds as an AABB for spatial queries.
        /// </summary>
        public AABB GetVisibleWorldAABB()
        {
            var bounds = GetVisibleWorldBounds();
            return new AABB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }

        /// <summary>
        /// Resets the transform to default (1:1 scale, no pan, origin at center).
        /// </summary>
        public void Reset()
        {
            _scale = 1.0;
            _panX = 0;
            _panY = 0;
            OnTransformChanged();
        }

        /// <summary>
        /// Zooms by a factor at the specified screen position (maintains world point under cursor).
        /// </summary>
        /// <param name="screenX">Screen X coordinate of zoom center</param>
        /// <param name="screenY">Screen Y coordinate of zoom center</param>
        /// <param name="zoomIn">True to zoom in, false to zoom out</param>
        /// <param name="zoomFactor">Zoom factor (default 1.1)</param>
        public void ZoomAtPoint(double screenX, double screenY, bool zoomIn, double zoomFactor = DefaultZoomFactor)
        {
            // Remember world position under cursor
            var worldPos = ScreenToWorld(screenX, screenY);

            // Apply zoom
            if (zoomIn)
                _scale *= zoomFactor;
            else
                _scale /= zoomFactor;

            _scale = Math.Clamp(_scale, MinZoom, MaxZoom);

            // Adjust pan to keep world point under cursor
            var newScreenPos = WorldToScreen(worldPos.X, worldPos.Y);
            _panX += screenX - newScreenPos.X;
            _panY += screenY - newScreenPos.Y;

            OnTransformChanged();
        }

        /// <summary>
        /// Zooms by a factor at the viewport center.
        /// </summary>
        public void Zoom(bool zoomIn, double zoomFactor = DefaultZoomFactor)
        {
            ZoomAtPoint(_viewportWidth / 2, _viewportHeight / 2, zoomIn, zoomFactor);
        }

        /// <summary>
        /// Sets the zoom level directly, centered on the viewport.
        /// </summary>
        public void SetZoom(double newScale)
        {
            var center = WorldCenter;
            _scale = Math.Clamp(newScale, MinZoom, MaxZoom);
            CenterOnWorldPoint(center.X, center.Y);
        }

        /// <summary>
        /// Pans the viewport by the specified screen delta.
        /// </summary>
        public void Pan(double deltaX, double deltaY)
        {
            _panX += deltaX;
            _panY += deltaY;
            OnTransformChanged();
        }

        /// <summary>
        /// Centers the viewport on the specified world point.
        /// </summary>
        public void CenterOnWorldPoint(double worldX, double worldY)
        {
            // Calculate pan needed to place worldX, worldY at screen center
            _panX = -worldX * _scale;
            _panY = worldY * _scale;
            OnTransformChanged();
        }

        /// <summary>
        /// Centers the viewport on the specified world point.
        /// </summary>
        public void CenterOnWorldPoint(Point worldPoint)
            => CenterOnWorldPoint(worldPoint.X, worldPoint.Y);

        /// <summary>
        /// Fits the viewport to show the given world bounds with optional padding.
        /// </summary>
        /// <param name="minX">Minimum X in world coordinates</param>
        /// <param name="minY">Minimum Y in world coordinates</param>
        /// <param name="maxX">Maximum X in world coordinates</param>
        /// <param name="maxY">Maximum Y in world coordinates</param>
        /// <param name="paddingPercent">Padding as percentage of viewport (0.1 = 10%)</param>
        public void FitToBounds(double minX, double minY, double maxX, double maxY, double paddingPercent = 0.1)
        {
            if (_viewportWidth <= 0 || _viewportHeight <= 0)
                return;

            double worldWidth = maxX - minX;
            double worldHeight = maxY - minY;

            // Handle degenerate cases
            if (worldWidth < GeometryTolerance.Epsilon && worldHeight < GeometryTolerance.Epsilon)
            {
                // Single point or very small: zoom to reasonable level and center
                _scale = 1.0;
                CenterOnWorldPoint((minX + maxX) / 2, (minY + maxY) / 2);
                return;
            }

            // Calculate scale to fit bounds with padding
            double paddedWidth = _viewportWidth * (1 - 2 * paddingPercent);
            double paddedHeight = _viewportHeight * (1 - 2 * paddingPercent);

            double scaleX = worldWidth > GeometryTolerance.Epsilon ? paddedWidth / worldWidth : MaxZoom;
            double scaleY = worldHeight > GeometryTolerance.Epsilon ? paddedHeight / worldHeight : MaxZoom;

            _scale = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);

            // Center on bounds center
            CenterOnWorldPoint((minX + maxX) / 2, (minY + maxY) / 2);
        }

        /// <summary>
        /// Fits the viewport to show all the given shapes.
        /// </summary>
        public void FitToShapes(IEnumerable<IDrawable> shapes, double paddingPercent = 0.1, double minWorldSize = 0)
        {
            var shapeList = shapes.ToList();
            if (!shapeList.Any())
            {
                Reset();
                return;
            }

            // Calculate combined bounds
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var shape in shapeList)
            {
                if (shape is Shape s)
                {
                    var (boundsMin, boundsMax) = s.GetBounds();
                    minX = Math.Min(minX, boundsMin.X);
                    minY = Math.Min(minY, boundsMin.Y);
                    maxX = Math.Max(maxX, boundsMax.X);
                    maxY = Math.Max(maxY, boundsMax.Y);
                }
            }

            if (minX > maxX || minY > maxY)
            {
                Reset();
                return;
            }

            // Apply minimum world size constraint
            if (minWorldSize > 0)
            {
                double width = maxX - minX;
                double height = maxY - minY;
                double centerX = (minX + maxX) / 2;
                double centerY = (minY + maxY) / 2;

                if (width < minWorldSize)
                {
                    minX = centerX - minWorldSize / 2;
                    maxX = centerX + minWorldSize / 2;
                }
                if (height < minWorldSize)
                {
                    minY = centerY - minWorldSize / 2;
                    maxY = centerY + minWorldSize / 2;
                }
            }

            FitToBounds(minX, minY, maxX, maxY, paddingPercent);
        }

        /// <summary>
        /// Gets a WPF Matrix representing the world-to-screen transform.
        /// Useful for transforming geometry or for direct rendering.
        /// </summary>
        public System.Windows.Media.Matrix GetWorldToScreenMatrix()
        {
            // Matrix operations: scale, flip Y, translate
            var matrix = new System.Windows.Media.Matrix();
            matrix.Scale(_scale, -_scale);  // Scale and flip Y
            matrix.Translate(_viewportWidth / 2 + _panX, _viewportHeight / 2 + _panY);
            return matrix;
        }

        /// <summary>
        /// Raises the TransformChanged event.
        /// </summary>
        protected virtual void OnTransformChanged()
        {
            TransformChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Creates a copy of this transform.
        /// </summary>
        public ViewportTransform Clone()
        {
            return new ViewportTransform
            {
                _scale = _scale,
                _panX = _panX,
                _panY = _panY,
                _viewportWidth = _viewportWidth,
                _viewportHeight = _viewportHeight
            };
        }

        /// <summary>
        /// Copies transform values from another instance.
        /// </summary>
        public void CopyFrom(ViewportTransform other)
        {
            _scale = other._scale;
            _panX = other._panX;
            _panY = other._panY;
            // Note: viewport dimensions are not copied as they're canvas-specific
            OnTransformChanged();
        }
    }
}
