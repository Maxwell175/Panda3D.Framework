using System;
using System.Reactive;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// A handle to an interval currently playing under the <see cref="IIntervalManager"/>. Hides the native
/// <see cref="CInterval"/> behind the controls a game needs, exposing completion as an awaitable observable:
/// <code>
/// intervals.Play(door.OpenAnim());                       // fire-and-forget
/// await intervals.Play(cutscene).Completed;              // resume when it finishes
/// intervals.Play(pulse).Completed.Subscribe(_ => …);     // or react to completion
/// </code>
/// A looped interval never completes, so never await one.
/// </summary>
public interface IPlayingInterval
{
    /// <summary>
    /// Fires once when playback reaches its final state, then completes. Late subscribers to an
    /// already-finished interval still receive it.
    /// </summary>
    IObservable<Unit> Completed { get; }

    /// <summary>Still active (not yet in its final state). A looped interval stays <see langword="true"/>.</summary>
    bool IsPlaying { get; }

    /// <summary>Playhead time in seconds; the setter seeks (poses at that time).</summary>
    double Time { get; set; }

    /// <summary>Playback rate multiplier (1 = normal, negative = reverse).</summary>
    double PlayRate { get; set; }

    /// <summary>Pause playback, holding the current time.</summary>
    void Pause();

    /// <summary>Resume from where it paused.</summary>
    void Resume();

    /// <summary>Jump to the end, firing completion.</summary>
    void Finish();

    /// <summary>The underlying native interval — escape hatch for controls beyond this handle.</summary>
    CInterval Native { get; }
}
