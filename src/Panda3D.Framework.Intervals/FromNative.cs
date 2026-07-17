using System;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>Adopt any raw native interval into the composition.</summary>
public sealed class FromNative : IInterval, INativeIntervalSource
{
    readonly CInterval _interval;
    public FromNative(CInterval interval) => _interval = interval ?? throw new ArgumentNullException(nameof(interval));
    public double Duration => _interval.GetDuration();
    // a specific native can't be rebuilt; replaying reuses it
    CInterval INativeIntervalSource.BuildNative() => _interval;
}
