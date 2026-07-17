using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>Implemented by intervals that flatten to a single native <c>CInterval</c>.</summary>
internal interface INativeIntervalSource
{
    /// <summary>Build a fresh native interval (so a description can be replayed).</summary>
    CInterval BuildNative();
}
