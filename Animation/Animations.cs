using System;
using Code2Viz.Geometry;

namespace Code2Viz.Animation
{
    /// <summary>
    /// Abstract base class for all animations.
    /// </summary>
    public abstract class Animation
    {
        public Shape Target { get; }
        public double StartTime { get; }
        public double Duration { get; }
        public Func<double, double> EasingFunction { get; set; } = EasingFunctions.Linear;

        protected Animation(Shape target, double startTime, double duration)
        {
            Target = target;
            StartTime = startTime;
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
        public DrawAnimation(Shape target, double startTime, double duration)
            : base(target, startTime, duration)
        {
        }

        public override void Apply(double t)
        {
            double easedT = EasingFunction(t);
            Target.DrawFactor = easedT;
        }
    }

    /// <summary>
    /// Animates moving a shape by a specified vector over time.
    /// </summary>
    public class MoveAnimation : Animation
    {
        private readonly VXYZ _displacement;
        private VXYZ? _initialPosition;

        public MoveAnimation(Shape target, VXYZ displacement, double startTime, double duration)
            : base(target, startTime, duration)
        {
            _displacement = displacement;
        }

        public override void Apply(double t)
        {
            if (_initialPosition == null)
            {
                _initialPosition = new VXYZ(Target.OffsetX, Target.OffsetY, 0);
            }

            double easedT = EasingFunction(t);
            Target.OffsetX = _initialPosition.X + _displacement.X * easedT;
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

        public RotateAnimation(Shape target, VPoint pivot, double angleDegrees, double startTime, double duration)
            : base(target, startTime, duration)
        {
            _pivot = pivot;
            _angleDegrees = angleDegrees;
        }

        public override void Apply(double t)
        {
            if (_initialRotation == null)
            {
                _initialRotation = Target.RotationAngle;
            }

            double easedT = EasingFunction(t);
            Target.RotationAngle = _initialRotation.Value + _angleDegrees * easedT;
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

        public FlipAnimation(Shape target, VLine mirrorAxis, double startTime, double duration)
            : base(target, startTime, duration)
        {
            _mirrorAxis = mirrorAxis;
        }

        public override void Apply(double t)
        {
            if (_initialFlipProgress == null)
            {
                _initialFlipProgress = Target.FlipProgress;
            }

            double easedT = EasingFunction(t);
            Target.FlipProgress = _initialFlipProgress.Value + (1.0 - _initialFlipProgress.Value) * easedT;
            Target.FlipAxis = _mirrorAxis;
        }
    }

    /// <summary>
    /// Animates fading in a shape from transparent to opaque.
    /// </summary>
    public class FadeInAnimation : Animation
    {
        private double? _initialOpacity;

        public FadeInAnimation(Shape target, double startTime, double duration)
            : base(target, startTime, duration)
        {
        }

        public override void Apply(double t)
        {
            if (_initialOpacity == null)
            {
                _initialOpacity = Target.Opacity;
            }

            double easedT = EasingFunction(t);
            // Fade from initial opacity (typically 0) to 1
            Target.Opacity = _initialOpacity.Value + (1.0 - _initialOpacity.Value) * easedT;
        }
    }

    /// <summary>
    /// Animates fading out a shape from opaque to transparent.
    /// </summary>
    public class FadeOutAnimation : Animation
    {
        private double? _initialOpacity;
        private double _targetOpacity;

        /// <summary>
        /// Creates a fade out animation.
        /// </summary>
        /// <param name="target">The shape to fade.</param>
        /// <param name="startTime">When the animation starts.</param>
        /// <param name="duration">How long the fade takes.</param>
        /// <param name="targetOpacity">The target opacity (default 0 = fully transparent).</param>
        public FadeOutAnimation(Shape target, double startTime, double duration, double targetOpacity = 0.0)
            : base(target, startTime, duration)
        {
            _targetOpacity = targetOpacity;
        }

        public override void Apply(double t)
        {
            if (_initialOpacity == null)
            {
                _initialOpacity = Target.Opacity;
            }

            double easedT = EasingFunction(t);
            // Interpolate from initial opacity to target opacity
            Target.Opacity = _initialOpacity.Value + (_targetOpacity - _initialOpacity.Value) * easedT;
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
