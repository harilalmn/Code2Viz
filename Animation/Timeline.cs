using System;
using System.Collections.Generic;
using System.Linq;
using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Animation
{
    internal class Timeline
    {
        public List<Shape> Shapes { get; private set; }
        public List<Animation> Animations { get; private set; }
        public double CurrentTime { get; private set; }
        public double Duration { get; set; }
        public bool IsPlaying { get; set; }
        public bool Repeat { get; set; }
        public double Speed { get; set; } = 1.0;
        public double Fps { get; set; } = 60;

        /// <summary>
        /// Creates a new timeline with no shapes. Shapes will be auto-drawn when animations are added.
        /// </summary>
        public Timeline()
        {
            Shapes = new List<Shape>();
            Animations = new List<Animation>();
            CurrentTime = 0;
        }

        /// <summary>
        /// Creates a new timeline with the specified shapes.
        /// </summary>
        [Obsolete("Use parameterless constructor instead. Shapes are now auto-drawn when animations are added.")]
        public Timeline(IEnumerable<Shape> shapes)
        {
            Shapes = shapes?.ToList() ?? new List<Shape>();
            Animations = new List<Animation>();
            CurrentTime = 0;
        }

        /// <summary>
        /// Activates this timeline for playback. Call this after adding all animations.
        /// </summary>
        public void Play()
        {
            IsPlaying = true;
            CanvasRenderer.Instance.ActiveTimeline = this;

            // Draw shapes from constructor (backward compatibility).
            // For new flow, shapes are auto-drawn in AddAnimation and this loop is a no-op
            // due to IsPlaced check in CanvasRenderer.AddShape.
            foreach (var shape in Shapes)
            {
                shape.Draw();
            }
        }

        /// <summary>
        /// Stops this timeline and removes it from the renderer.
        /// </summary>
        public void Stop()
        {
            IsPlaying = false;
            if (CanvasRenderer.Instance.ActiveTimeline == this)
            {
                CanvasRenderer.Instance.ActiveTimeline = null;
            }
        }

        public void AddAnimation(Animation animation, double startTime)
        {
            animation.StartTime = startTime;
            Animations.Add(animation);

            // Auto-draw shape if not already placed (skip for non-Shape animations like ObjectPropertyAnimation)
            if (animation.Target != null && !animation.Target.IsPlaced)
            {
                animation.Target.Draw();
            }

            // Auto-extend duration if needed
            if (startTime + animation.Duration > Duration)
            {
                Duration = startTime + animation.Duration;
            }
        }

        public void Update(double time)
        {
            CurrentTime = time;

            if (Repeat && Duration > 0)
            {
                CurrentTime %= Duration;
            }
            
            // Clamp time if not repeating
            if (!Repeat)
            {
                if (CurrentTime < 0) CurrentTime = 0;
                if (CurrentTime > Duration) CurrentTime = Duration;
            }

            foreach (var anim in Animations)
            {
                // Check if animation is active at CurrentTime
                // or if we should apply the final state if time > end
                // or initial state if time < start
                
                double t = (CurrentTime - anim.StartTime) / anim.Duration;
                
                // Clamp t to 0..1 for standard processing
                // Or allow over/under shoot if desired (usually 0..1)
                bool isActive = t >= 0 && t <= 1;
                bool isPast = t > 1;
                bool isFuture = t < 0;

                if (isActive)
                {
                    anim.Apply(t);
                }
                else if (isPast)
                {
                    // Apply end state to ensure we don't glitch when moving fast
                    anim.Apply(1.0);
                }
                else if (isFuture)
                {
                    // Pass the negative t value so animations know they haven't started yet
                    // and can avoid capturing initial state prematurely
                    anim.Apply(t);
                }
            }
        }
    }
}
