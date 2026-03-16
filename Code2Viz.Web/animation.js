// Code2Viz Web - Animation System
// Timeline-based animation with multiple animation types and easing functions

import { VPoint, getRegistry, VColor } from './geometry/index.js';

// ============================================================================
// Easing Functions
// ============================================================================
export const Easing = {
    linear: t => t,
    easeInQuad: t => t * t,
    easeOutQuad: t => t * (2 - t),
    easeInOutQuad: t => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t,
    easeInCubic: t => t * t * t,
    easeOutCubic: t => (--t) * t * t + 1,
    easeInOutCubic: t => t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1,
    easeInSine: t => 1 - Math.cos(t * Math.PI / 2),
    easeOutSine: t => Math.sin(t * Math.PI / 2),
    easeInOutSine: t => -(Math.cos(Math.PI * t) - 1) / 2,
    easeInExpo: t => t === 0 ? 0 : Math.pow(2, 10 * (t - 1)),
    easeOutExpo: t => t === 1 ? 1 : 1 - Math.pow(2, -10 * t),
    easeOutBounce: t => {
        if (t < 1 / 2.75) return 7.5625 * t * t;
        if (t < 2 / 2.75) return 7.5625 * (t -= 1.5 / 2.75) * t + 0.75;
        if (t < 2.5 / 2.75) return 7.5625 * (t -= 2.25 / 2.75) * t + 0.9375;
        return 7.5625 * (t -= 2.625 / 2.75) * t + 0.984375;
    },
    easeInElastic: t => t === 0 ? 0 : t === 1 ? 1 : -Math.pow(2, 10 * t - 10) * Math.sin((t * 10 - 10.75) * (2 * Math.PI / 3)),
    easeOutElastic: t => t === 0 ? 0 : t === 1 ? 1 : Math.pow(2, -10 * t) * Math.sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1,
};

// ============================================================================
// Animation Types
// ============================================================================
class Animation {
    constructor(shape, duration, easing = 'linear', delay = 0) {
        this.shape = shape;
        this.duration = duration;
        this.delay = delay;
        this.easing = typeof easing === 'function' ? easing : (Easing[easing] || Easing.linear);
        this._started = false;
        this._complete = false;
    }

    get isComplete() { return this._complete; }

    start() { this._started = true; }

    update(t) {
        if (this._complete) return;
        const localT = Math.max(0, Math.min(1, (t - this.delay) / this.duration));
        if (localT <= 0) return;
        const eased = this.easing(localT);
        this.apply(eased);
        if (localT >= 1) this._complete = true;
    }

    apply(t) { /* override */ }
    reset() { this._complete = false; this._started = false; }
}

export class DrawAnimation extends Animation {
    constructor(shape, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._origDrawFactor = shape.drawFactor;
    }
    apply(t) { this.shape.drawFactor = t; }
    reset() { super.reset(); this.shape.drawFactor = 0; }
}

export class MoveAnimation extends Animation {
    constructor(shape, dx, dy, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._dx = dx; this._dy = dy;
        this._startX = shape.offsetX;
        this._startY = shape.offsetY;
    }
    apply(t) {
        this.shape.offsetX = this._startX + this._dx * t;
        this.shape.offsetY = this._startY + this._dy * t;
    }
    reset() { super.reset(); this.shape.offsetX = this._startX; this.shape.offsetY = this._startY; }
}

export class FadeAnimation extends Animation {
    constructor(shape, fromOpacity, toOpacity, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._from = fromOpacity; this._to = toOpacity;
    }
    apply(t) { this.shape.opacity = this._from + (this._to - this._from) * t; }
    reset() { super.reset(); this.shape.opacity = this._from; }
}

export class ScaleAnimation extends Animation {
    constructor(shape, fromScale, toScale, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._from = fromScale; this._to = toScale;
        this._origLineWeight = shape.lineWeight;
    }
    apply(t) {
        const s = this._from + (this._to - this._from) * t;
        this.shape.lineWeight = this._origLineWeight * s;
    }
    reset() { super.reset(); this.shape.lineWeight = this._origLineWeight; }
}

export class RotateAnimation extends Animation {
    constructor(shape, angleDeg, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._angle = angleDeg;
        this._startAngle = shape.rotationAngle;
    }
    apply(t) { this.shape.rotationAngle = this._startAngle + this._angle * t; }
    reset() { super.reset(); this.shape.rotationAngle = this._startAngle; }
}

export class ColorAnimation extends Animation {
    constructor(shape, fromColor, toColor, duration, easing, delay) {
        super(shape, duration, easing, delay);
        this._fromRGB = this._parseColor(fromColor);
        this._toRGB = this._parseColor(toColor);
    }
    _parseColor(color) {
        const ctx = document.createElement('canvas').getContext('2d');
        ctx.fillStyle = color;
        const hex = ctx.fillStyle;
        if (hex.startsWith('#')) {
            return [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];
        }
        return [255, 255, 255];
    }
    apply(t) {
        const r = Math.round(this._fromRGB[0] + (this._toRGB[0] - this._fromRGB[0]) * t);
        const g = Math.round(this._fromRGB[1] + (this._toRGB[1] - this._fromRGB[1]) * t);
        const b = Math.round(this._fromRGB[2] + (this._toRGB[2] - this._fromRGB[2]) * t);
        this.shape.color = `rgb(${r},${g},${b})`;
    }
}

// ============================================================================
// Timeline
// ============================================================================
export class Timeline {
    constructor() {
        this._animations = []; // { animation, startTime }
        this._duration = 0;
    }

    add(animation, startTime = this._duration) {
        this._animations.push({ animation, startTime });
        this._duration = Math.max(this._duration, startTime + animation.duration + animation.delay);
        return this;
    }

    sequence(...animations) {
        let t = this._duration;
        for (const anim of animations) {
            this._animations.push({ animation: anim, startTime: t });
            t += anim.duration + anim.delay;
        }
        this._duration = t;
        return this;
    }

    parallel(...animations) {
        const t = this._duration;
        for (const anim of animations) {
            this._animations.push({ animation: anim, startTime: t });
            this._duration = Math.max(this._duration, t + anim.duration + anim.delay);
        }
        return this;
    }

    get duration() { return this._duration; }

    update(time) {
        let allComplete = true;
        for (const { animation, startTime } of this._animations) {
            const localTime = time - startTime;
            if (localTime < 0) { allComplete = false; continue; }
            animation.update(localTime / (animation.duration + animation.delay));
            if (!animation.isComplete) allComplete = false;
        }
        return allComplete;
    }

    reset() {
        for (const { animation } of this._animations) animation.reset();
    }
}

// ============================================================================
// Animator (global animation controller)
// ============================================================================
export class Animator {
    constructor(renderer) {
        this._renderer = renderer;
        this._timeline = null;
        this._playing = false;
        this._startTime = 0;
        this._elapsed = 0;
        this._speed = 1.0;
        this._loop = false;
        this._onUpdate = null;
        this._onComplete = null;
        this._rafId = null;
    }

    get isPlaying() { return this._playing; }
    get elapsed() { return this._elapsed; }
    get speed() { return this._speed; }
    set speed(v) { this._speed = v; }
    get loop() { return this._loop; }
    set loop(v) { this._loop = v; }
    get timeline() { return this._timeline; }

    onUpdate(callback) { this._onUpdate = callback; }
    onComplete(callback) { this._onComplete = callback; }

    setTimeline(timeline) {
        this.stop();
        this._timeline = timeline;
    }

    play() {
        if (!this._timeline) return;
        this._playing = true;
        this._startTime = performance.now() - this._elapsed * 1000;
        this._tick();
    }

    pause() {
        this._playing = false;
        if (this._rafId) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
        }
    }

    stop() {
        this.pause();
        this._elapsed = 0;
        if (this._timeline) this._timeline.reset();
        this._renderer.render();
    }

    _tick() {
        if (!this._playing) return;

        const now = performance.now();
        this._elapsed = ((now - this._startTime) / 1000) * this._speed;

        if (this._timeline) {
            const duration = this._timeline.duration;
            let t = this._elapsed;

            if (t >= duration) {
                if (this._loop) {
                    this._timeline.reset();
                    this._startTime = now;
                    this._elapsed = 0;
                    t = 0;
                } else {
                    t = duration;
                    this._timeline.update(t);
                    this._renderer.render();
                    this._playing = false;
                    if (this._onComplete) this._onComplete();
                    if (this._onUpdate) this._onUpdate(t, duration);
                    return;
                }
            }

            this._timeline.update(t);
            this._renderer.render();
            if (this._onUpdate) this._onUpdate(t, duration);
        }

        this._rafId = requestAnimationFrame(() => this._tick());
    }
}
