using System;
using System.Collections.Generic;
using Interrogate;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

/// <summary>
/// The pump drains the one global event queue and routes by name to C# subscribers, parsing
/// <c>EventParameter</c>s into boxed values (06).
/// </summary>
public sealed class EventPumpTests
{
    static void DrainResidual(EventPump pump)
    {
        // Clear anything left on the shared global queue from other tests before asserting.
        pump.PumpOnce();
    }

    [Fact]
    public void RoutesEventByNameWithParsedParameters()
    {
        var pump = new EventPump();
        DrainResidual(pump);

        var received = new List<NamedEvent>();
        using var sub = pump.Observe("unit-test-event").Subscribe(received.Add);

        var e = new Event("unit-test-event");
        e.AddParameter(new EventParameter(42));
        e.AddParameter(new EventParameter(2.5));
        e.AddParameter(new EventParameter("hello"));
        EventQueue.GetGlobalEventQueue().QueueEvent(e);

        pump.PumpOnce();

        var evt = Assert.Single(received);
        Assert.Equal("unit-test-event", evt.Name);
        Assert.Equal(3, evt.Parameters.Count);
        Assert.Equal(42, Assert.IsType<int>(evt.Parameters[0]));
        Assert.Equal(2.5, Assert.IsType<double>(evt.Parameters[1]));
        Assert.Equal("hello", Assert.IsType<string>(evt.Parameters[2]));
    }

    [Fact]
    public void DoesNotRouteToUnrelatedName()
    {
        var pump = new EventPump();
        DrainResidual(pump);

        var received = new List<NamedEvent>();
        using var sub = pump.Observe("wanted").Subscribe(received.Add);

        EventQueue.GetGlobalEventQueue().QueueEvent(new Event("other"));
        pump.PumpOnce();

        Assert.Empty(received);
    }

    [Fact]
    public void SendQueuesThroughTheSamePump()
    {
        var pump = new EventPump();
        DrainResidual(pump);

        var received = new List<NamedEvent>();
        using var sub = pump.Observe("sent").Subscribe(received.Add);

        pump.Send("sent", 7, "x");
        pump.PumpOnce();

        var evt = Assert.Single(received);
        Assert.Equal(7, evt.Parameters[0]);
        Assert.Equal("x", evt.Parameters[1]);
    }

    [Fact]
    public void ParsesNativeObjectParameters()
    {
        var pump = new EventPump();
        DrainResidual(pump);

        var received = new List<NamedEvent>();
        using var sub = pump.Observe("native-payload").Subscribe(received.Add);
        var node = new PandaNode("payload-node");

        pump.Send("native-payload", node);
        pump.PumpOnce();

        var evt = Assert.Single(received);
        var native = Assert.IsAssignableFrom<INativeObject>(evt.Parameters[0]);
        var parsed = native.CastTo<PandaNode>();

        Assert.NotNull(parsed);
        Assert.True(parsed.Equals(node));
    }

    [Fact]
    public void UnsubscribeStopsDelivery()
    {
        var pump = new EventPump();
        DrainResidual(pump);

        var received = new List<NamedEvent>();
        var sub = pump.Observe("toggle").Subscribe(received.Add);
        sub.Dispose();

        EventQueue.GetGlobalEventQueue().QueueEvent(new Event("toggle"));
        pump.PumpOnce();

        Assert.Empty(received);
    }
}
