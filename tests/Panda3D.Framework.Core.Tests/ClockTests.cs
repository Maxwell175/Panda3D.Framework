using Microsoft.Extensions.DependencyInjection;
using Panda3D.Core;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

public sealed class ClockTests
{
    [Fact]
    public void GameClockReflectsGlobalClock()
    {
        var services = new ServiceCollection();
        services.AddScheduler();
        using var sp = services.BuildServiceProvider();

        var clock = sp.GetRequiredService<IGameClock>();
        var native = ClockObject.GetGlobalClock();

        long before = clock.FrameCount;
        native.Tick();

        Assert.True(clock.FrameCount >= before + 1);
        Assert.Equal(native.GetFrameTime(), clock.FrameTime, precision: 6);
    }
}
