using System;
using System.Linq.Expressions;
using System.Reflection;
using Code2Viz.Geometry;

namespace Code2Viz.Animation
{
    /// <summary>
    /// Abstract base class for all animations.
    /// </summary>
    public abstract class Animation
    {
        public Shape? Target { get; }
        public double StartTime { get; internal set; }
        public double Duration { get; }
        public Func<double, double> EasingFunction { get; set; } = EasingFunctions.Linear;

        /// <summary>
        /// Optional name for the animation (e.g., variable name from code).
        /// </summary>
        public string? Name { get; set; }

        protected Animation(Shape target, double duration)
        {
            Target = target;
            Duration = duration;
        }

        /// <summary>
        /// Constructor for animations that don't target a specific Shape (e.g., ObjectPropertyAnimation).
        /// </summary>
        protected Animation(double duration)
        {
            Target = null;
            Duration = duration;
        }

        /// <summary>
        /// Apply the animation at normalized time t (0 to 1).
        /// </summary>
        public abstract void Apply(double t);
    }

    /// <summary>
    /// Animates the DrawFactor property to progressively draw a shape from 0% to 100%.
    /// </summary>
    public class DrawAnimation : Animation
    {
        public DrawAnimation(Shape target, double duration)
            : base(target, duration)
        {
            // Set DrawFactor to 0 so shape starts invisible (including VGroup children)
            SetDrawFactorRecursive(target, 0);
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            SetDrawFactorRecursive(Target, easedT);
        }

        private static void SetDrawFactorRecursive(Shape shape, double drawFactor)
        {
            shape.DrawFactor = drawFactor;
            if (shape is VGroup group)
            {
                foreach (var child in group.Shapes)
                    SetDrawFactorRecursive(child, drawFactor);
            }
        }
    }

    /// <summary>
    /// Animates moving a shape by a specified vector over time.
    /// </summary>
    public class MoveAnimation : Animation
    {
        private readonly VXYZ _displacement;
        private VXYZ? _initialPosition;
        private bool _hasStarted;

        public MoveAnimation(Shape target, VXYZ displacement, double duration)
            : base(target, duration)
        {
            _displacement = displacement;
        }

        public override void Apply(double t)
        {
            // Only capture initial position when animation actually starts (t >= 0 for first time)
            if (!_hasStarted && t >= 0)
            {
                _initialPosition = new VXYZ(Target.OffsetX, Target.OffsetY, 0);
                _hasStarted = true;
            }

            // Don't apply anything if we haven't started yet
            if (!_hasStarted)
                return;

            double easedT = EasingFunction(Math.Clamp(t, 0, 1));
            Target.OffsetX = _initialPosition!.X + _displacement.X * easedT;
            Target.OffsetY = _initialPosition.Y + _displacement.Y * easedT;
        }
    }

    /// <summary>
    /// Animates rotating a shape around a pivot point by a specified angle.
    /// </summary>
    public class RotateAnimation : Animation
    {
        private readonly VPoint _pivot;
        private readonly double _angleDegrees;
        private double? _initialRotation;
        private bool _hasStarted;

        public RotateAnimation(Shape target, VPoint pivot, double angleDegrees, double duration)
            : base(target, duration)
        {
            _pivot = pivot;
            _angleDegrees = angleDegrees;
        }

        public override void Apply(double t)
        {
            // Only capture initial rotation when animation actually starts (t >= 0 for first time)
            // Don't capture when being called for future state (t < 0 normalized from timeline)
            if (!_hasStarted && t >= 0)
            {
                _initialRotation = Target.RotationAngle;
                _hasStarted = true;
            }

            // Don't apply anything if we haven't started yet
            if (!_hasStarted)
                return;

            double easedT = EasingFunction(Math.Clamp(t, 0, 1));
            Target.RotationAngle = _initialRotation!.Value + _angleDegrees * easedT;
            Target.RotationPivot = _pivot;
        }
    }

    /// <summary>
    /// Animates flipping (mirroring) a shape across a specified axis line.
    /// </summary>
    public class FlipAnimation : Animation
    {
        private readonly VLine _mirrorAxis;
        private double? _initialFlipProgress;
        private bool _hasStarted;

        public FlipAnimation(Shape target, VLine mirrorAxis, double duration)
            : base(target, duration)
        {
            _mirrorAxis = mirrorAxis;
        }

        public override void Apply(double t)
        {
            // Only capture initial flip progress when animation actually starts (t >= 0 for first time)
            if (!_hasStarted && t >= 0)
            {
                _initialFlipProgress = Target.FlipProgress;
                _hasStarted = true;
            }

            // Don't apply anything if we haven't started yet
            if (!_hasStarted)
                return;

            double easedT = EasingFunction(Math.Clamp(t, 0, 1));
            Target.FlipProgress = _initialFlipProgress!.Value + (1.0 - _initialFlipProgress.Value) * easedT;
            Target.FlipAxis = _mirrorAxis;
        }
    }

    /// <summary>
    /// Animates fading in a shape from transparent to opaque.
    /// </summary>
    public class FadeInAnimation : Animation
    {
        public FadeInAnimation(Shape target, double duration)
            : base(target, duration)
        {
            // Set opacity to 0 for fade-in to work (including VGroup children)
            SetOpacityRecursive(target, 0);
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            // Fade from 0 to 1
            SetOpacityRecursive(Target, easedT);
        }

        private static void SetOpacityRecursive(Shape shape, double opacity)
        {
            shape.Opacity = opacity;
            if (shape is VGroup group)
            {
                foreach (var child in group.Shapes)
                    SetOpacityRecursive(child, opacity);
            }
        }
    }

    /// <summary>
    /// Animates fading out a shape from opaque to transparent.
    /// </summary>
    public class FadeOutAnimation : Animation
    {
        private double _targetOpacity;

        /// <summary>
        /// Creates a fade out animation.
        /// </summary>
        /// <param name="target">The shape to fade.</param>
        /// <param name="duration">How long the fade takes.</param>
        /// <param name="targetOpacity">The target opacity (default 0 = fully transparent).</param>
        public FadeOutAnimation(Shape target, double duration, double targetOpacity = 0.0)
            : base(target, duration)
        {
            _targetOpacity = targetOpacity;
            // Set opacity to 1 for fade-out to work (including VGroup children)
            SetOpacityRecursive(target, 1);
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            // Fade from 1 to target opacity (usually 0)
            double opacity = 1.0 + (_targetOpacity - 1.0) * easedT;
            SetOpacityRecursive(Target, opacity);
        }

        private static void SetOpacityRecursive(Shape shape, double opacity)
        {
            shape.Opacity = opacity;
            if (shape is VGroup group)
            {
                foreach (var child in group.Shapes)
                    SetOpacityRecursive(child, opacity);
            }
        }
    }

    /// <summary>
    /// Animates any numeric (double) property on a shape using an expression to identify the property.
    /// </summary>
    /// <typeparam name="T">The shape type.</typeparam>
    public class ValueAnimation<T> : Animation where T : Shape
    {
        private readonly PropertyInfo _property;
        private readonly double _startValue;
        private readonly double _endValue;

        /// <summary>
        /// Creates a value animation that interpolates a property between start and end values.
        /// </summary>
        /// <param name="target">The shape whose property to animate.</param>
        /// <param name="propertySelector">Expression selecting the property, e.g. c => c.Radius.</param>
        /// <param name="startValue">The value at the beginning of the animation.</param>
        /// <param name="endValue">The value at the end of the animation.</param>
        /// <param name="duration">Duration in seconds.</param>
        public ValueAnimation(T target, Expression<Func<T, double>> propertySelector, double startValue, double endValue, double duration)
            : base(target, duration)
        {
            _startValue = startValue;
            _endValue = endValue;

            // Extract PropertyInfo from the expression
            if (propertySelector.Body is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo)
            {
                _property = propInfo;
            }
            else
            {
                throw new ArgumentException("propertySelector must be a simple property access expression, e.g. c => c.Radius.");
            }

            // Set the initial value
            _property.SetValue(target, _startValue);
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            double value = _startValue + (_endValue - _startValue) * easedT;
            _property.SetValue(Target, value);
        }
    }

    /// <summary>
    /// Animates any numeric (double) property on an arbitrary object using an expression to identify the property.
    /// Unlike ValueAnimation, this is not limited to Shape targets.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    public class ObjectPropertyAnimation<T> : Animation where T : class
    {
        private readonly T _targetObject;
        private readonly PropertyInfo _property;
        private readonly double _startValue;
        private readonly double _endValue;

        /// <summary>
        /// Creates an object property animation that interpolates a property between start and end values.
        /// </summary>
        /// <param name="targetObject">The object whose property to animate.</param>
        /// <param name="propertySelector">Expression selecting the property, e.g. w => w.Rotation.</param>
        /// <param name="startValue">The value at the beginning of the animation.</param>
        /// <param name="endValue">The value at the end of the animation.</param>
        /// <param name="duration">Duration in seconds.</param>
        public ObjectPropertyAnimation(T targetObject, Expression<Func<T, double>> propertySelector, double startValue, double endValue, double duration)
            : base(duration)
        {
            _targetObject = targetObject;
            _startValue = startValue;
            _endValue = endValue;

            // Extract PropertyInfo from the expression
            if (propertySelector.Body is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo)
            {
                _property = propInfo;
            }
            else
            {
                throw new ArgumentException("propertySelector must be a simple property access expression, e.g. w => w.Rotation.");
            }

            // Set the initial value
            _property.SetValue(_targetObject, _startValue);
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            double value = _startValue + (_endValue - _startValue) * easedT;
            _property.SetValue(_targetObject, value);
        }
    }

    /// <summary>
    /// Provides common easing functions.
    /// </summary>
    public static class EasingFunctions
    {
        public static double Linear(double t) => t;

        public static double EaseInQuad(double t) => t * t;

        public static double EaseOutQuad(double t) => t * (2 - t);

        public static double EaseInOutQuad(double t)
        {
            if (t < 0.5)
                return 2 * t * t;
            return -1 + (4 - 2 * t) * t;
        }

        public static double EaseInCubic(double t) => t * t * t;

        public static double EaseOutCubic(double t)
        {
            t--;
            return t * t * t + 1;
        }

        public static double EaseInOutCubic(double t)
        {
            if (t < 0.5)
                return 4 * t * t * t;
            t = 2 * t - 2;
            return (t * t * t + 2) / 2;
        }
    }
}
