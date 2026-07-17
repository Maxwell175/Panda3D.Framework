using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Core.Tests;

/// <summary>
/// End-to-end validation of the Poll-only loop: hosted services materialize native sorted tasks,
/// the bootstrap coroutine resumes each epoch, the clock advances headlessly, sorted tasks run in
/// order, and shutdown removes everything.
/// </summary>
public sealed class HostingLoopTests
{
    /// <summary>Shared observation sink resolved by the bootstrap and the sorted tasks.</summary>
    sealed class FrameProbe
    {
        public int Frames;
        public bool ClockAdvanced;
        public bool EventDeliveredBeforeGameplay;
        public readonly List<string> Order = new();
    }

    /// <summary>An <see cref="IBootstrap"/> that runs an arbitrary coroutine body.</summary>
    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    static IGameApplicationBuilder NewBuilder()
    {
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();       // TickClock on: default chain advances the clock headlessly
        builder.Services.AddScheduler();
        return builder;
    }

    [Fact]
    public void RunsBootstrapAdvancesClockAndStops()
    {
        var probe = new FrameProbe();
        var builder = NewBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        app.Run();   // blocks until the bootstrap stops the app

        Assert.Equal(6, probe.Frames);
        Assert.True(probe.ClockAdvanced, "clock should advance headlessly via the ticking chain");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var clock = sp.GetRequiredService<IGameClock>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<FrameProbe>();

            double t0 = clock.FrameTime;
            for (int i = 0; i < 6; i++)
                await PandaTask.NextFrame();

            probe.Frames = 6;
            probe.ClockAdvanced = clock.FrameTime > t0;
            life.StopApplication();
        }
    }

    [Fact]
    public void InjectedDelegateBootstrapRunsCoroutine()
    {
        var probe = new FrameProbe();
        var builder = NewBuilder();
        builder.Services.AddSingleton(probe);

        // The AddBootstrap(Delegate) overload: any number of injected parameters, and a plain
        // async (Task-returning) lambda that still drives the frame loop via PandaTask.NextFrame.
        builder.Services.AddBootstrap(async (IGameClock clock, IHostApplicationLifetime life, FrameProbe p) =>
        {
            double t0 = clock.FrameTime;
            for (int i = 0; i < 6; i++)
                await PandaTask.NextFrame();

            p.Frames = 6;
            p.ClockAdvanced = clock.FrameTime > t0;
            life.StopApplication();
        });

        var app = builder.Build();
        app.Run();

        Assert.Equal(6, probe.Frames);
        Assert.True(probe.ClockAdvanced, "the injected coroutine should have driven the frame loop");
    }

    [Fact]
    public void SortedTasksRunInFrameSlotOrder()
    {
        var probe = new FrameProbe();
        var builder = NewBuilder();
        builder.Services.AddSingleton(probe);

        // Deliberately registered out of order — the sort, not registration, decides execution order.
        builder.Services.AddFrameTask("igLoop", FrameSlots.Render, _ => { probe.Order.Add("render"); return TaskResult.Continue; });
        builder.Services.AddFrameTask("dataLoop", FrameSlots.DataLoop, _ => { probe.Order.Add("data"); return TaskResult.Continue; });
        builder.Services.AddFrameTask("gameplay", FrameSlots.Gameplay, _ => { probe.Order.Add("gameplay"); return TaskResult.Continue; });

        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => StopAfterAsync(sp, 5)));

        var app = builder.Build();
        app.Run();

        int data = probe.Order.IndexOf("data");
        int gameplay = probe.Order.IndexOf("gameplay");
        int render = probe.Order.IndexOf("render");

        Assert.True(data >= 0 && gameplay >= 0 && render >= 0, "all three tasks should have run");
        Assert.True(data < gameplay, $"dataLoop({FrameSlots.DataLoop}) before gameplay({FrameSlots.Gameplay})");
        Assert.True(gameplay < render, $"gameplay({FrameSlots.Gameplay}) before igLoop({FrameSlots.Render})");
    }

    [Fact]
    public void EventPumpRunsBeforeDefaultGameplayTasks()
    {
        var probe = new FrameProbe();
        var builder = NewBuilder();
        builder.Services.AddSingleton(probe);

        bool delivered = false;
        bool sent = false;
        builder.Services.AddFrameTask("queue-event", FrameSlots.DataLoop, ctx =>
        {
            if (!sent)
            {
                sent = true;
                ctx.Services.GetRequiredService<INamedEventBus>().Send("ordered-event");
            }

            return TaskResult.Continue;
        });

        builder.Services.AddFrameTask("read-event", FrameSlots.Gameplay, ctx =>
        {
            probe.EventDeliveredBeforeGameplay = delivered;
            ctx.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            return TaskResult.Done;
        });

        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => PandaTask.CompletedTask));

        var app = builder.Build();
        using var sub = app.Services.GetRequiredService<INamedEventBus>()
            .Observe("ordered-event")
            .Subscribe(_ => delivered = true);

        app.Run();

        Assert.True(
            probe.EventDeliveredBeforeGameplay,
            $"eventManager({FrameSlots.Events}) should drain after dataLoop({FrameSlots.DataLoop}) and before gameplay({FrameSlots.Gameplay})");
    }

    [Fact]
    public void SchedulerTaskRunsThenStopsWhenDisposed()
    {
        int countedWhileActive = 0;
        int countedAfterDispose = 0;
        var builder = NewBuilder();

        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(async () =>
        {
            var scheduler = sp.GetRequiredService<IFrameScheduler>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();

            bool active = true;
            var handle = scheduler.AddFrameTask(_ =>
            {
                if (active) countedWhileActive++;
                else countedAfterDispose++;
                return TaskResult.Continue;
            }, sort: FrameSlots.Gameplay);

            for (int i = 0; i < 4; i++) await PandaTask.NextFrame();
            active = false;
            handle.Dispose();                 // remove the native task
            for (int i = 0; i < 4; i++) await PandaTask.NextFrame();

            life.StopApplication();
        }));

        var app = builder.Build();
        app.Run();

        Assert.True(countedWhileActive > 0, "the scheduled task should have run while active");
        Assert.Equal(0, countedAfterDispose);
    }

    static async PandaTask StopAfterAsync(IServiceProvider sp, int frames)
    {
        var life = sp.GetRequiredService<IHostApplicationLifetime>();
        for (int i = 0; i < frames; i++)
            await PandaTask.NextFrame();
        life.StopApplication();
    }
}
