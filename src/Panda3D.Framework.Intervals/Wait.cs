using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>A timed pause — native <c>WaitInterval</c>.</summary>
public sealed class Wait : IInterval, INativeIntervalSource
{
    readonly double _seconds;
    public Wait(double seconds) => _seconds = seconds;
    public double Duration => _seconds;
    CInterval INativeIntervalSource.BuildNative() => new WaitInterval(_seconds);
}
