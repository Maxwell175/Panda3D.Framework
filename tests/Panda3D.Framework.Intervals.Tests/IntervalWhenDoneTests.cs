using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Intervals.Tests;

/// <summary>
/// Validates the awaitable-completion path end-to-end: a played interval's done-event flows through
/// the real event pump and completes the <c>WhenDone</c> task, inside the actual host loop.
/// </summary>
public sealed class IntervalWhenDoneTests
{
    sealed class Probe { public bool Completed; public double NodeX; }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void WhenDoneCompletesAfterIntervalFinishes()
    {
        // Drive the loop with a fixed dt (non-real-time) so the interval steps deterministically —
        // at t=0 the epoch it's played (ivalLoop sort 20 runs after the bootstrap at sort 0), then to
        // completion the next epoch — regardless of wall-clock speed or CPU contention. The engine
        // clock is a shared process global, so this is restored to real-time after the run.
        var clock = Panda3D.Core.ClockObject.GetGlobalClock();
        clock.Reset();
        clock.SetMode(Panda3D.Core.ClockObjectMode.MNonRealTime);
        clock.SetDt(0.01);

        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddIntervals();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        try
        {
            app.Run();
        }
        finally
        {
            clock.SetMode(Panda3D.Core.ClockObjectMode.MNormal);
            clock.Reset();
        }

        Assert.True(probe.Completed, "WhenDone should have completed via the done-event pump");
        Assert.True(probe.NodeX > 4.9, $"node should have reached the target (was {probe.NodeX})");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var intervals = sp.GetRequiredService<IIntervalManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var node = scene.Root.AttachNewNode("mover");
            // dt=0.01 per epoch, duration 0.05 → completes deterministically in ~6 epochs.
            var handle = intervals.Play(node.PosTo(new LVecBase3f(5, 0, 0), 0.05, from: new LVecBase3f(0, 0, 0)));
            bool done = false;
            using var sub = handle.Completed.Subscribe(_ => done = true);

            for (int i = 0; i < 4000 && !done; i++)
                await PandaTask.NextFrame();

            probe.Completed = done;
            probe.NodeX = node.GetX();
            life.StopApplication();
        }
    }
}
