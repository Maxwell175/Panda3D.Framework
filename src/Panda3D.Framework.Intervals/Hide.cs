using System;
using Panda3D.Core;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>Hide a node — native <c>HideInterval</c> (instantaneous).</summary>
public sealed class Hide : IInterval, INativeIntervalSource
{
    readonly NodePath _node;
    public Hide(NodePath node) => _node = node ?? throw new ArgumentNullException(nameof(node));
    public double Duration => 0;
    CInterval INativeIntervalSource.BuildNative() => new HideInterval(_node);
}
