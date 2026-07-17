using System;
using System.Threading;

namespace Panda3D.Framework;

/// <summary>
/// The argument handed to a per-frame task, whether registered at setup-time or through the
/// gameplay-facing <see cref="IFrameScheduler"/>.
/// </summary>
/// <param name="Services">The root service provider (resolve scoped state via <c>IServiceScopeFactory</c>).</param>
/// <param name="Dt">This frame's delta time, as reported by <see cref="IGameClock.Dt"/>.</param>
/// <param name="Stopping">Signalled when the host is shutting down.</param>
public readonly record struct FrameContext(IServiceProvider Services, double Dt, CancellationToken Stopping);

/// <summary>Whether a per-frame task keeps running or is removed after this epoch.</summary>
public enum TaskResult
{
    /// <summary>Run again next epoch.</summary>
    Continue,

    /// <summary>Remove the task.</summary>
    Done,
}
