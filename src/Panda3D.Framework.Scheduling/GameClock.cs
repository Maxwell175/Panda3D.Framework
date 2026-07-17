using Panda3D.Core;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// <see cref="IGameClock"/> over Panda's global <c>ClockObject</c>. Read-only: ticking is owned by the
/// task chain or <c>RenderFrame</c>, never the accessor.
/// </summary>
internal sealed class GameClock : IGameClock
{
    readonly ClockObject _clock = ClockObject.GetGlobalClock();

    public double Dt => _clock.GetDt();
    public double FrameTime => _clock.GetFrameTime();
    public double RealTime => _clock.GetRealTime();
    public long FrameCount => _clock.GetFrameCount();
}
