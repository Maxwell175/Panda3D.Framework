using System;
using Panda3D.Core;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>Show a node — native <c>ShowInterval</c> (instantaneous).</summary>
public sealed class Show : IInterval, INativeIntervalSource
{
    readonly NodePath _node;
    public Show(NodePath node) => _node = node ?? throw new ArgumentNullException(nameof(node));
    public double Duration => 0;
    CInterval INativeIntervalSource.BuildNative() => new ShowInterval(_node);
}
