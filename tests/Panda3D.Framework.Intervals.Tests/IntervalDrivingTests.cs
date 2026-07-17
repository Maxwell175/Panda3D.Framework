using System;
using System.Collections.Generic;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Intervals;
using Xunit;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Panda3D.Framework.Intervals.Tests;

/// <summary>
/// Drives the interval manager deterministically by putting the global clock in non-real-time mode
/// (fixed dt per tick), so timing is exact and headless. Exercises native lerps, composition
/// (Sequence/Parallel), scrubbing, and the managed external-interval dispatch (Call / Lerp&lt;T&gt;).
/// </summary>
public sealed class IntervalDrivingTests
{
    /// <summary>Minimal bus — these tests drive the manager directly and never await done-events.</summary>
    sealed class FakeBus : INamedEventBus
    {
        public IObservable<NamedEvent> Observe(string name) => System.Reactive.Linq.Observable.Empty<NamedEvent>();
        public IDisposable Subscribe(string name, Action<NamedEvent> handler) => System.Reactive.Disposables.Disposable.Empty;
        public void Send(string name, params object[] parameters) { }
    }

    /// <summary>
    /// Runs <paramref name="body"/> with the shared global clock in fixed-dt mode. On exit it resets
    /// the clock to a clean real-time state, so this test can't pollute the global clock's frame-time
    /// for a later real-time test (the engine globals are shared across the whole test process).
    /// </summary>
    static void WithFixedClock(double dt, Action<Action> body)
    {
        var clock = ClockObject.GetGlobalClock();
        clock.SetMode(ClockObjectMode.MNonRealTime);
        clock.SetDt(dt);
        try
        {
            body(() => clock.Tick());
        }
        finally
        {
            clock.SetMode(ClockObjectMode.MNormal);
            clock.Reset();   // realign frame-time with real time for subsequent tests
        }
    }

    [Fact]
    public void ScrubPosesTheNodeAtT()
    {
        var node = new NodePath("scrub");
        using var mgr = new IntervalManager(new FakeBus());

        var play = mgr.Play(node.PosTo(new LVecBase3f(10, 0, 0), 1.0, from: new LVecBase3f(0, 0, 0)));

        play.Time = 0.5;
        Assert.Equal(5.0, node.GetX(), 3);

        play.Time = 1.0;
        Assert.Equal(10.0, node.GetX(), 3);

        play.Time = 0.0;
        Assert.Equal(0.0, node.GetX(), 3);
    }

    [Fact]
    public void HandleExposesControlsOverNativeInterval()
    {
        var node = new NodePath("controlled");
        using var mgr = new IntervalManager(new FakeBus());

        var play = mgr.Play(node.PosTo(new LVecBase3f(10, 0, 0), 1.0, from: new LVecBase3f(0, 0, 0)));

        Assert.True(play.IsPlaying);                       // started, not yet final
        Assert.NotNull(play.Native);                       // escape hatch to the CInterval

        play.PlayRate = 2.0;                               // round-trips through the native interval
        Assert.Equal(2.0, play.PlayRate, 3);

        play.Pause();
        Assert.True(play.IsPlaying);                       // paused is still "active", not finished

        play.Finish();                                     // jump to the end
        Assert.False(play.IsPlaying);                      // now final
        Assert.Equal(10.0, node.GetX(), 3);                // posed at the target
    }

    [Fact]
    public void PlayedLerpAdvancesToTargetOverTime()
    {
        var node = new NodePath("mover");
        using var mgr = new IntervalManager(new FakeBus());

        WithFixedClock(0.25, tick =>
        {
            mgr.Play(node.PosTo(new LVecBase3f(8, 0, 0), 1.0, from: new LVecBase3f(0, 0, 0)));
            // Step-first, matching the real loop: the interval initializes at t=0 the frame it's played,
            // then the clock advances for subsequent steps.
            for (int i = 0; i < 6; i++) { mgr.Step(); tick(); }   // 1.5s of 1.0s interval
            Assert.Equal(8.0, node.GetX(), 2);
        });
    }

    [Fact]
    public void ManagedCallFiresExactlyOnce()
    {
        int count = 0;
        using var mgr = new IntervalManager(new FakeBus());

        WithFixedClock(0.5, tick =>
        {
            mgr.Play(new Sequence(new Call(() => count++)));
            for (int i = 0; i < 4; i++) { mgr.Step(); tick(); }
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void ManagedLerpStepsAndCompletes()
    {
        float value = -1;
        var samples = new List<float>();
        using var mgr = new IntervalManager(new FakeBus());

        WithFixedClock(0.2, tick =>
        {
            mgr.Play(new Parallel(new Lerp<float>(0f, 10f, 1.0, v => { value = v; samples.Add(v); })));
            for (int i = 0; i < 7; i++) { mgr.Step(); tick(); }   // 1.4s of 1.0s
        });

        Assert.Equal(10f, value, 2);                  // Complete() set the final value
        Assert.True(samples.Count >= 3, "Step should have been called multiple times");
        Assert.True(samples[^1] >= samples[0], "value should progress toward the target");
    }

    [Fact]
    public void SoundIntervalInitializesStepsAndStopsNativeSound()
    {
        var audio = AudioManager.CreateAudioManager();
        try
        {
            var sound = audio.GetNullSound();
            var interval = new SoundInterval(sound, duration: 2.0, volume: 0.25f, startTime: 1.0);

            interval.Initialize(0.5);
            Assert.Equal(2.0, interval.Duration, 3);
            interval.Step(1.25);
            interval.Complete();

            Assert.NotEqual(AudioSoundSoundStatus.Playing, sound.Status());
        }
        finally
        {
            audio.Shutdown();
        }
    }

    [Fact]
    public void SequenceRunsItemsInOrder()
    {
        var log = new List<string>();
        using var mgr = new IntervalManager(new FakeBus());

        WithFixedClock(0.1, tick =>
        {
            mgr.Play(new Sequence(
                new Call(() => log.Add("a")),
                new Wait(0.2),
                new Call(() => log.Add("b"))));

            for (int i = 0; i < 6; i++) { mgr.Step(); tick(); }
        });

        Assert.Equal(new[] { "a", "b" }, log);
    }
}
