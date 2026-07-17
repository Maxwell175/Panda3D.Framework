using System;
using Panda3D.Async;

namespace Panda3D.Framework;

/// <summary>
/// The gameplay-facing scheduling seam (replaces <c>taskMgr.add</c> / <c>doMethodLater</c>). Tasks
/// carry an explicit <c>sort</c> on the <see cref="FrameSlots"/> scale and return a disposable handle.
/// </summary>
public interface IFrameScheduler
{
    /// <summary>Register a recurring per-frame task at <paramref name="sort"/> (default <see cref="FrameSlots.Gameplay"/>).</summary>
    IScheduledTask AddFrameTask(Func<FrameContext, TaskResult> task, int sort = FrameSlots.Gameplay, string? name = null);

    /// <summary>Run <paramref name="action"/> once after <paramref name="delay"/> — <c>doMethodLater</c>.</summary>
    IScheduledTask AddTimed(TimeSpan delay, Action action);

    /// <summary>
    /// Deterministic fixed-rate updates: an accumulator that calls <paramref name="step"/> (with the fixed
    /// dt) zero or more times per frame to drain whole fixed intervals, regardless of frame rate.
    /// </summary>
    IScheduledTask AddFixedStep(double hz, Action<double> step, int sort = FrameSlots.Gameplay);

    /// <summary>Coroutine-style yield: resume after <paramref name="n"/> frames on the current chain.</summary>
    PandaTask DelayFrames(int n);
}
