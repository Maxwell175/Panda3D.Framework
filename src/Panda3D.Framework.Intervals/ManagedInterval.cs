namespace Panda3D.Framework.Intervals;

/// <summary>
/// Game logic that participates inside a timeline via the engine's external-interval mechanism. The
/// manager relays the native event types to these hooks whether the timeline is playing forward,
/// scrubbed, or reversed.
/// </summary>
public abstract class ManagedInterval : IInterval
{
    // open-ended by default so a zero-duration event (e.g. a Call at t=0) still fires when the timeline
    // initializes on or seeks past its point; the engine drops non-open-ended instant events there.
    protected ManagedInterval(double duration, bool openEnded = true, string? name = null)
    {
        Duration = duration;
        OpenEnded = openEnded;
        Name = name;
    }

    public double Duration { get; }
    internal bool OpenEnded { get; }
    public string? Name { get; }

    /// <summary>Called when the interval starts (or is entered while scrubbing). <c>ET_initialize</c>.</summary>
    public virtual void Initialize(double t) { }

    /// <summary>Called with <paramref name="t"/> in [0, Duration] while playing and when scrubbed. <c>ET_step</c>.</summary>
    public abstract void Step(double t);

    /// <summary>Called when the interval reaches its end. <c>ET_finalize</c>.</summary>
    public virtual void Complete() { }

    /// <summary>Called when the timeline is interrupted (auto-pause/finish). <c>ET_interrupt</c>.</summary>
    public virtual void Interrupt() { }
}
