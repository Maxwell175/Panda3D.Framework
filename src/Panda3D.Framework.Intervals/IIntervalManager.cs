using System;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Owns a constructed <c>CIntervalManager</c> and the per-frame step task. <see cref="Play"/> and
/// <see cref="Loop"/> hand back an <see cref="IPlayingInterval"/> — a thin handle over the native
/// <c>CInterval</c> that exposes an awaitable, observable <see cref="IPlayingInterval.Completed"/>.
/// </summary>
public interface IIntervalManager : IDisposable
{
    /// <summary>Flatten (composites → one native <c>CMetaInterval</c>), attach, <c>Start()</c>; returns a playback handle.</summary>
    IPlayingInterval Play(IInterval interval);

    /// <summary>As <see cref="Play"/>, but native <c>Loop()</c> — the handle never completes.</summary>
    IPlayingInterval Loop(IInterval interval);

    /// <summary>Pause/finish everything tagged auto-pause/auto-finish (scene transitions).</summary>
    void Interrupt();
}
