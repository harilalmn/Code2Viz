using System;
using System.Collections.Generic;
using System.Linq;
using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Commands
{
    /// <summary>
    /// Command for adding a shape to the canvas.
    /// </summary>
    public class AddShapeCommand : ICommand
    {
        private readonly Shape _shape;
        private readonly RenderCanvas _canvas;

        public string Description { get; }

        public AddShapeCommand(Shape shape, RenderCanvas canvas, string? description = null)
        {
            _shape = shape ?? throw new ArgumentNullException(nameof(shape));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            Description = description ?? $"Add {shape.GetType().Name}";
        }

        public void Execute()
        {
            _shape.Draw();
            _canvas.Refresh();
        }

        public void Undo()
        {
            // Remove shape from renderer
            var shapes = CanvasRenderer.Instance.GetShapes().ToList();
            if (shapes.Contains(_shape))
            {
                // We need to clear and re-add all shapes except this one
                // This is inefficient but works with current architecture
                CanvasRenderer.Instance.Clear();
                foreach (var s in shapes)
                {
                    if (s != _shape && s is Shape shape)
                    {
                        shape.IsPlaced = false;
                        shape.Draw();
                    }
                }
            }
            _shape.IsPlaced = false;
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }

    /// <summary>
    /// Command for deleting shapes from the canvas.
    /// </summary>
    public class DeleteShapesCommand : ICommand
    {
        private readonly List<Shape> _shapes;
        private readonly RenderCanvas _canvas;

        public string Description { get; }

        public DeleteShapesCommand(IEnumerable<Shape> shapes, RenderCanvas canvas)
        {
            _shapes = shapes.ToList();
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            if (_shapes.Count == 1)
                Description = $"Delete {_shapes[0].GetType().Name}";
            else
                Description = $"Delete {_shapes.Count} shapes";
        }

        public DeleteShapesCommand(Shape shape, RenderCanvas canvas)
            : this(new[] { shape }, canvas)
        {
        }

        public void Execute()
        {
            // Remove shapes from renderer
            var allShapes = CanvasRenderer.Instance.GetShapes().ToList();
            CanvasRenderer.Instance.Clear();

            foreach (var s in allShapes)
            {
                if (!_shapes.Contains(s) && s is Shape shape)
                {
                    shape.IsPlaced = false;
                    shape.Draw();
                }
            }

            foreach (var shape in _shapes)
            {
                shape.IsPlaced = false;
            }

            _canvas.Refresh();
        }

        public void Undo()
        {
            // Re-add the deleted shapes
            foreach (var shape in _shapes)
            {
                shape.IsPlaced = false;
                shape.Draw();
            }
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }

    /// <summary>
    /// Command for moving shapes by a displacement vector.
    /// Supports merging for continuous drag operations.
    /// </summary>
    public class MoveShapesCommand : ICommand
    {
        private readonly List<Shape> _shapes;
        private readonly RenderCanvas _canvas;
        private VXYZ _totalDisplacement;
        private readonly DateTime _createdAt;

        public string Description { get; private set; } = string.Empty;

        /// <summary>
        /// Time window for merging consecutive move commands (milliseconds).
        /// </summary>
        public static int MergeWindowMs { get; set; } = 500;

        public MoveShapesCommand(IEnumerable<Shape> shapes, VXYZ displacement, RenderCanvas canvas)
        {
            _shapes = shapes.ToList();
            _totalDisplacement = displacement;
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _createdAt = DateTime.UtcNow;

            UpdateDescription();
        }

        public MoveShapesCommand(Shape shape, VXYZ displacement, RenderCanvas canvas)
            : this(new[] { shape }, displacement, canvas)
        {
        }

        private void UpdateDescription()
        {
            if (_shapes.Count == 1)
                Description = $"Move {_shapes[0].GetType().Name}";
            else
                Description = $"Move {_shapes.Count} shapes";
        }

        public void Execute()
        {
            foreach (var shape in _shapes)
            {
                shape.Move(_totalDisplacement);
            }
            _canvas.Refresh();
        }

        public void Undo()
        {
            // Move by negative displacement
            var reverseDisplacement = new VXYZ(-_totalDisplacement.X, -_totalDisplacement.Y, -_totalDisplacement.Z);
            foreach (var shape in _shapes)
            {
                shape.Move(reverseDisplacement);
            }
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other)
        {
            if (other is not MoveShapesCommand moveCmd)
                return false;

            // Can merge if same shapes and within time window
            if (moveCmd._shapes.Count != _shapes.Count)
                return false;

            if (!_shapes.All(s => moveCmd._shapes.Contains(s)))
                return false;

            var timeDiff = (DateTime.UtcNow - _createdAt).TotalMilliseconds;
            return timeDiff < MergeWindowMs;
        }

        public void MergeWith(ICommand other)
        {
            if (other is MoveShapesCommand moveCmd)
            {
                // Add the new displacement to our total
                // Note: We don't re-execute because the move was already applied
                _totalDisplacement = new VXYZ(
                    _totalDisplacement.X + moveCmd._totalDisplacement.X,
                    _totalDisplacement.Y + moveCmd._totalDisplacement.Y,
                    _totalDisplacement.Z + moveCmd._totalDisplacement.Z
                );
            }
        }
    }

    /// <summary>
    /// Command for modifying a shape's property.
    /// </summary>
    public class ModifyPropertyCommand<T> : ICommand
    {
        private readonly Shape _shape;
        private readonly string _propertyName;
        private readonly T _oldValue;
        private readonly T _newValue;
        private readonly Action<T> _setter;
        private readonly RenderCanvas _canvas;

        public string Description { get; }

        public ModifyPropertyCommand(
            Shape shape,
            string propertyName,
            T oldValue,
            T newValue,
            Action<T> setter,
            RenderCanvas canvas)
        {
            _shape = shape ?? throw new ArgumentNullException(nameof(shape));
            _propertyName = propertyName;
            _oldValue = oldValue;
            _newValue = newValue;
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            Description = $"Modify {shape.GetType().Name}.{propertyName}";
        }

        public void Execute()
        {
            _setter(_newValue);
            _canvas.Refresh();
        }

        public void Undo()
        {
            _setter(_oldValue);
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }

    /// <summary>
    /// Command for rotating shapes around a pivot point.
    /// </summary>
    public class RotateShapesCommand : ICommand
    {
        private readonly List<Shape> _shapes;
        private readonly VPoint _pivot;
        private readonly double _angleDegrees;
        private readonly RenderCanvas _canvas;

        public string Description { get; }

        public RotateShapesCommand(IEnumerable<Shape> shapes, VPoint pivot, double angleDegrees, RenderCanvas canvas)
        {
            _shapes = shapes.ToList();
            _pivot = pivot;
            _angleDegrees = angleDegrees;
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            if (_shapes.Count == 1)
                Description = $"Rotate {_shapes[0].GetType().Name}";
            else
                Description = $"Rotate {_shapes.Count} shapes";
        }

        public void Execute()
        {
            foreach (var shape in _shapes)
            {
                shape.Rotate(_pivot, _angleDegrees);
            }
            _canvas.Refresh();
        }

        public void Undo()
        {
            foreach (var shape in _shapes)
            {
                shape.Rotate(_pivot, -_angleDegrees);
            }
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }

    /// <summary>
    /// Command for scaling shapes from a center point.
    /// </summary>
    public class ScaleShapesCommand : ICommand
    {
        private readonly List<Shape> _shapes;
        private readonly VPoint _center;
        private readonly double _factor;
        private readonly RenderCanvas _canvas;

        public string Description { get; }

        public ScaleShapesCommand(IEnumerable<Shape> shapes, VPoint center, double factor, RenderCanvas canvas)
        {
            _shapes = shapes.ToList();
            _center = center;
            _factor = factor;
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            if (_shapes.Count == 1)
                Description = $"Scale {_shapes[0].GetType().Name}";
            else
                Description = $"Scale {_shapes.Count} shapes";
        }

        public void Execute()
        {
            foreach (var shape in _shapes)
            {
                shape.Scale(_center, _factor);
            }
            _canvas.Refresh();
        }

        public void Undo()
        {
            // Scale by inverse factor
            foreach (var shape in _shapes)
            {
                shape.Scale(_center, 1.0 / _factor);
            }
            _canvas.Refresh();
        }

        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }
}
