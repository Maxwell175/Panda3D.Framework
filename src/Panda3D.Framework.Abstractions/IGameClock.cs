namespace Panda3D.Framework;

/// <summary>
/// The injected view of the engine's global clock — gameplay reads <see cref="Dt"/> here rather than
/// from an ambient <c>globalClock</c>.
/// </summary>
public interface IGameClock
{
    /// <summary>Seconds elapsed since the previous frame.</summary>
    double Dt { get; }

    /// <summary>The time, in seconds, of the current frame (advances once per epoch).</summary>
    double FrameTime { get; }

    /// <summary>Actual wall-clock seconds since the clock started, independent of frame pacing.</summary>
    double RealTime { get; }

    /// <summary>Number of frames elapsed since the clock started.</summary>
    long FrameCount { get; }
}
