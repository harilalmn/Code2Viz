using System;
using System.Collections.Generic;

namespace Code2Viz.Animation
{
    /// <summary>
    /// Manages animation sequencing automatically. Animations are added sequentially by default,
    /// or in parallel when added as a list.
    /// </summary>
    public class Animator
    {
        private Timeline _timeline = new Timeline();
        private double _nextStartTime = 0;

        /// <summary>
        /// Gets the total duration of all animations.
        /// </summary>
        public double Duration => _timeline.Duration;

        /// <summary>
        /// Gets or sets whether the animation should repeat after completing.
        /// </summary>
        public bool Repeat
        {
            get => _timeline.Repeat;
            set => _timeline.Repeat = value;
        }

        /// <summary>
        /// Gets or sets the playback speed multiplier (1.0 = normal speed).
        /// </summary>
        public double Speed
        {
            get => _timeline.Speed;
            set => _timeline.Speed = value;
        }

        /// <summary>
        /// Adds a single animation to play sequentially after any previous animations.
        /// </summary>
        /// <param name="animation">The animation to add.</param>
        public void AddToAnimations(Animation animation)
        {
            _timeline.AddAnimation(animation, _nextStartTime);
            _nextStartTime += animation.Duration;
        }

        /// <summary>
        /// Adds multiple animations to play in parallel, all starting at the same time.
        /// The next sequential animation will start after the longest of these completes.
        /// </summary>
        /// <param name="animations">The animations to play in parallel.</param>
        public void AddToAnimations(List<Animation> animations)
        {
            double maxDuration = 0;
            foreach (var anim in animations)
            {
                _timeline.AddAnimation(anim, _nextStartTime);
                maxDuration = Math.Max(maxDuration, anim.Duration);
            }
            _nextStartTime += maxDuration;
        }

        /// <summary>
        /// Starts playback of all animations.
        /// </summary>
        public void Animate()
        {
            _timeline.Play();
        }

        /// <summary>
        /// Stops playback of all animations.
        /// </summary>
        public void Stop()
        {
            _timeline.Stop();
        }
    }
}
