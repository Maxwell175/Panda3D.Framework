using System;
using Panda3D.Framework.Events;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

public sealed class NamedEventTests
{
    [Fact]
    public void TypedAccessorsReadAndGuardParameters()
    {
        var e = new NamedEvent("hit", new object[] { 42, "target" });

        Assert.Equal(2, e.Count);
        Assert.Equal(42, e.Get<int>(0));
        Assert.Equal("target", e.Get<string>(1));

        Assert.True(e.TryGet<int>(0, out var n));
        Assert.Equal(42, n);

        Assert.False(e.TryGet<string>(0, out _));   // wrong type
        Assert.False(e.TryGet<int>(5, out _));       // out of range
        Assert.Throws<InvalidCastException>(() => e.Get<string>(0));
    }
}
