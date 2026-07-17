using System;
using Panda3D.Core;
using Panda3D.Framework.Intervals;

namespace Panda3D.Framework.Actors;

/// <summary>
/// A managed interval that poses a named animation on an actor part from the timeline's time, so it
/// composes in <c>Sequence</c>/<c>Parallel</c> and scrubs deterministically. Optionally loops, constrains
/// the loop to a frame range, and plays a sub-range at a scaled rate.
/// </summary>
public sealed class ActorInterval : ManagedInterval
{
    readonly IAnimControl _control;
    readonly bool _loop;
    readonly bool _constrainedLoop;
    readonly double _startFrame;
    readonly double _endFrame;
    readonly double _frameRate;

    /// <summary>
    /// Build an interval over <paramref name="anim"/> on <paramref name="part"/> of <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The actor to pose.</param>
    /// <param name="anim">The animation clip name.</param>
    /// <param name="part">The part to animate (defaults to the whole actor).</param>
    /// <param name="loop">Loop the animation over the interval's duration.</param>
    /// <param name="constrainedLoop">When looping, wrap within the <paramref name="startFrame"/>/<paramref name="endFrame"/> range.</param>
    /// <param name="duration">Explicit duration in seconds; defaults to the frame range at the play rate.</param>
    /// <param name="startFrame">First frame of the played range (defaults to 0).</param>
    /// <param name="endFrame">Last frame of the played range (defaults to the clip's last frame).</param>
    /// <param name="playRate">Rate multiplier applied to the clip's native frame rate.</param>
    public ActorInterval(
        IActor actor,
        string anim,
        string part = ActorDefaults.DefaultPart,
        bool loop = false,
        bool constrainedLoop = false,
        double? duration = null,
        int? startFrame = null,
        int? endFrame = null,
        double playRate = 1)
        : base(ResolveDuration(actor, anim, part, duration, startFrame, endFrame, playRate), name: $"actor-{anim}")
    {
        ArgumentNullException.ThrowIfNull(actor);
        _control = actor.Anim(anim, part);
        _loop = loop;
        _constrainedLoop = constrainedLoop;
        _startFrame = startFrame ?? 0;
        _endFrame = endFrame ?? Math.Max(0, _control.GetNumFrames() - 1);
        _frameRate = _control.GetFrameRate() * playRate;
    }

    public override void Initialize(double t) => Step(t);

    public override void Step(double t)
    {
        double frame = _startFrame + Math.Clamp(t, 0, Duration) * _frameRate;
        double min = Math.Min(_startFrame, _endFrame);
        double max = Math.Max(_startFrame, _endFrame);

        if (_constrainedLoop)
            frame = WrapInclusive(frame, min, max);
        else if (_loop)
            frame = WrapExclusive(frame, 0, Math.Max(1, _control.GetNumFrames()));
        else
            frame = Math.Clamp(frame, min, max);

        _control.Pose(frame);
    }

    public override void Complete()
    {
        if (!_loop && !_constrainedLoop)
            _control.Pose(_endFrame);
    }

    static double ResolveDuration(IActor actor, string anim, string part, double? duration, int? startFrame, int? endFrame, double playRate)
    {
        if (duration is not null) return duration.Value;
        var control = actor.Anim(anim, part);
        double rate = Math.Abs(control.GetFrameRate() * playRate);
        if (rate <= 0) return 0;
        double start = startFrame ?? 0;
        double end = endFrame ?? Math.Max(0, control.GetNumFrames() - 1);
        return Math.Abs(end - start) / rate;
    }

    static double WrapInclusive(double value, double min, double max)
    {
        double span = max - min + 1;
        if (span <= 0) return min;
        return min + PositiveMod(value - min, span);
    }

    static double WrapExclusive(double value, double min, double max)
    {
        double span = max - min;
        if (span <= 0) return min;
        return min + PositiveMod(value - min, span);
    }

    static double PositiveMod(double value, double span)
    {
        double result = value % span;
        return result < 0 ? result + span : result;
    }
}
