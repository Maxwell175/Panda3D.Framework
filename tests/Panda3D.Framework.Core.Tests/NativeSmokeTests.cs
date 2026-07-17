using Panda3D.Core;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

/// <summary>
/// Confirms the native Panda runtime loads and the core globals the framework relies on are usable
/// headlessly (no graphics pipe / window).
/// </summary>
public sealed class NativeSmokeTests
{
    [Fact]
    public void CanCreateNodePath()
    {
        var np = new NodePath("render");
        Assert.False(np.IsEmpty());
        Assert.Equal("render", np.GetName());
    }

    [Fact]
    public void GlobalClockTicks()
    {
        var clock = ClockObject.GetGlobalClock();
        long before = clock.GetFrameCount();
        clock.Tick();
        Assert.True(clock.GetFrameCount() >= before + 1);
    }

    [Fact]
    public void GlobalTaskManagerAndEventQueueResolve()
    {
        Assert.NotNull(AsyncTaskManager.GetGlobalPtr());
        Assert.NotNull(EventQueue.GetGlobalEventQueue());
        Assert.NotNull(Panda3D.Core.EventHandler.GetGlobalEventHandler());
    }

    /// <summary>
    /// Round-trips a public data member on a native value-struct through the real runtime. These
    /// members (<c>InputDevice::AxisState::value</c>, <c>BatteryData::level</c>, ...) were silently
    /// dropped by the interrogate C# generator until the wrapper-fallback fix — pass 1 always emitted
    /// the native accessor wrappers, but pass 2 rebuilt no property for them. BatteryData is the
    /// smallest publicly-constructible struct that exercises that same code path (the one that now
    /// also surfaces analog gamepad axis <c>Value</c>); a green assertion proves the generated
    /// getter/setter P/Invokes bind to live native symbols and marshal correctly.
    /// </summary>
    [Fact]
    public void NativeValueStructDataMembersRoundTrip()
    {
        var battery = new InputDevice.BatteryData();
        battery.MaxLevel = 100;
        battery.Level = 42;

        Assert.Equal((short)100, battery.MaxLevel);
        Assert.Equal((short)42, battery.Level);
    }

    [Fact]
    public void CollisionNodeCollideMaskAliasOverridesPandaNodeAlias()
    {
        var method = typeof(CollisionNode).GetMethod(
            nameof(CollisionNode.SetIntoCollideMask),
            [typeof(BitMask32)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(CollisionNode), method!.DeclaringType);
        Assert.Equal(typeof(PandaNode), method.GetBaseDefinition().DeclaringType);
    }
}
