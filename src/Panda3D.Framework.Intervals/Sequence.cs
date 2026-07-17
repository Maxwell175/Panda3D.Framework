using System;
using System.Collections.Generic;
using System.Linq;

namespace Panda3D.Framework.Intervals;

/// <summary>Items run one after another. Flattened into a native <c>CMetaInterval</c> level at <c>Play</c>.</summary>
public sealed class Sequence : IInterval
{
    public Sequence(params IInterval[] items) : this(null, items) { }

    public Sequence(string? name, params IInterval[] items)
    {
        Name = name;
        Items = new List<IInterval>(items ?? Array.Empty<IInterval>());
    }

    public string? Name { get; }

    /// <summary>Mutable; edits after a <c>Play</c> take effect on the next <c>Play</c> (reflatten).</summary>
    public IList<IInterval> Items { get; }

    public double Duration => Items.Sum(i => i.Duration);
}
