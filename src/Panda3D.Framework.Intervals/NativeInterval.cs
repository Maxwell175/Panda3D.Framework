using System;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// A native-backed interval whose native is (re)built from a factory each time it is flattened, so a
/// description can be replayed. Node lerps produce these.
/// </summary>
internal sealed class NativeInterval : IInterval, INativeIntervalSource
{
    readonly Func<CInterval> _build;
    public NativeInterval(double duration, Func<CInterval> build)
    {
        Duration = duration;
        _build = build;
    }
    public double Duration { get; }
    public CInterval BuildNative() => _build();
}
