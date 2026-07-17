using System;
using System.Collections.Generic;
using System.Linq;

namespace Panda3D.Framework.Intervals;

/// <summary>Items start together. Flattened into a native <c>CMetaInterval</c> level at <c>Play</c>.</summary>
public sealed class Parallel : IInterval
{
    public Parallel(params IInterval[] items) : this(null, items) { }

    public Parallel(string? name, params IInterval[] items)
    {
        Name = name;
        Items = new List<IInterval>(items ?? Array.Empty<IInterval>());
    }

    public string? Name { get; }
    public IList<IInterval> Items { get; }

    public double Duration => Items.Count == 0 ? 0 : Items.Max(i => i.Duration);
}
