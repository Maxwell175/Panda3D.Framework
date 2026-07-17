using System;
using System.Collections.Generic;
using System.Threading;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Direct;
using Panda3D.Framework.Events;
using RelStart = Panda3D.Direct.CMetaIntervalRelativeStart;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Owns a constructed <c>CIntervalManager</c>, flattens managed descriptions into native
/// <c>CMetaInterval</c>s, steps them each frame, and relays external-interval events to
/// <see cref="ManagedInterval"/>s.
/// </summary>
internal sealed class IntervalManager : IIntervalManager
{
    // process-global so done-event names never collide across managers
    static int _doneCounter;

    readonly CIntervalManager _mgr = new();
    readonly INamedEventBus _bus;
    readonly Dictionary<int, PlayState> _active = new();

    public IntervalManager(INamedEventBus bus) => _bus = bus;

    public IPlayingInterval Play(IInterval interval) => new PlayingInterval(Launch(interval, loop: false), WhenDone);
    public IPlayingInterval Loop(IInterval interval) => new PlayingInterval(Launch(interval, loop: true), WhenDone);

    CInterval Launch(IInterval interval, bool loop)
    {
        ArgumentNullException.ThrowIfNull(interval);

        var (playable, meta, ext) = Compile(interval);
        playable.SetDoneEvent($"ival-done-{Interlocked.Increment(ref _doneCounter)}");

        // external=true only when there are managed children whose events we must relay.
        int index = _mgr.AddCInterval(playable, ext.Count > 0);
        _active[index] = new PlayState(meta, ext);

        if (loop) playable.Loop();
        else playable.Start();
        return playable;
    }

    // completes when the done-event fires; an already-finished interval completes immediately
    PandaTask WhenDone(CInterval playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        if (playing.GetState() == CIntervalState.SFinal) return PandaTask.CompletedTask;

        string doneEvent = playing.GetDoneEvent();
        if (string.IsNullOrEmpty(doneEvent))
            throw new InvalidOperationException("Interval has no done-event; play it through this manager first.");

        var tcs = new PandaTaskCompletionSource();
        IDisposable? sub = null;
        sub = _bus.Subscribe(doneEvent, _ =>
        {
            sub?.Dispose();
            tcs.TrySetResult();
        });

        // guard the race between the state check and the subscription
        if (playing.GetState() == CIntervalState.SFinal)
        {
            sub.Dispose();
            tcs.TrySetResult();
        }
        return tcs.Task;
    }

    public void Interrupt() => _mgr.Interrupt();

    /// <summary>Native step + managed-callback drain. Called by the step task at <see cref="FrameSlots.Intervals"/>.</summary>
    public void Step()
    {
        _mgr.Step();

        // removals first: an interval may re-add itself while posting its final event
        int idx = _mgr.GetNextRemoval();
        while (idx >= 0)
        {
            if (_active.TryGetValue(idx, out var st))
            {
                st.PostEvents();
                _active.Remove(idx);
            }
            idx = _mgr.GetNextRemoval();
        }

        idx = _mgr.GetNextEvent();
        while (idx >= 0)
        {
            if (_active.TryGetValue(idx, out var st))
                st.PostEvents();
            idx = _mgr.GetNextEvent();
        }
    }

    int _disposed;

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _mgr.Interrupt();
        _active.Clear();
    }

    // ---- flattening ----

    static (CInterval playable, CMetaInterval? meta, List<ManagedInterval> ext) Compile(IInterval interval)
    {
        var ext = new List<ManagedInterval>();

        switch (interval)
        {
            case Sequence s:
            {
                var meta = new CMetaInterval("root");
                foreach (var c in s.Items) Flatten(c, meta, ext, RelStart.RsPreviousEnd);
                return (meta, meta, ext);
            }
            case Parallel p:
            {
                var meta = new CMetaInterval("root");
                foreach (var c in p.Items) Flatten(c, meta, ext, RelStart.RsPreviousBegin);
                return (meta, meta, ext);
            }
            case ManagedInterval:
            {
                var meta = new CMetaInterval("root");
                Flatten(interval, meta, ext, RelStart.RsPreviousEnd);
                return (meta, meta, ext);
            }
            case INativeIntervalSource nat:
                return (nat.BuildNative(), null, ext);
            default:
                throw new NotSupportedException($"Unsupported interval type {interval.GetType()}");
        }
    }

    static void Flatten(IInterval iv, CMetaInterval meta, List<ManagedInterval> ext, RelStart relTo)
    {
        switch (iv)
        {
            case Sequence s:
                meta.PushLevel(s.Name ?? "seq", 0, relTo);
                foreach (var c in s.Items) Flatten(c, meta, ext, RelStart.RsPreviousEnd);
                meta.PopLevel();
                break;
            case Parallel p:
                meta.PushLevel(p.Name ?? "par", 0, relTo);
                foreach (var c in p.Items) Flatten(c, meta, ext, RelStart.RsPreviousBegin);
                meta.PopLevel();
                break;
            case ManagedInterval m:
                int extIndex = ext.Count;
                ext.Add(m);
                meta.AddExtIndex(extIndex, m.Name ?? "managed", m.Duration, m.OpenEnded, 0, relTo);
                break;
            case INativeIntervalSource src:
                meta.AddCInterval(src.BuildNative(), 0, relTo);
                break;
            default:
                throw new NotSupportedException($"Unsupported interval type {iv.GetType()}");
        }
    }

    /// <summary>Per-play bookkeeping: the flattened meta (if any) and its ordered managed children.</summary>
    sealed class PlayState
    {
        readonly CMetaInterval? _meta;
        readonly List<ManagedInterval> _ext;

        public PlayState(CMetaInterval? meta, List<ManagedInterval> ext)
        {
            _meta = meta;
            _ext = ext;
        }

        public void PostEvents()
        {
            if (_meta is null) return;
            while (_meta.IsEventReady())
            {
                int extIndex = _meta.GetEventIndex();
                double t = _meta.GetEventT();
                var type = _meta.GetEventType();
                _meta.PopEvent();
                if (extIndex >= 0 && extIndex < _ext.Count)
                    Dispatch(_ext[extIndex], t, type);
            }
        }

        static void Dispatch(ManagedInterval m, double t, CIntervalEventType type)
        {
            switch (type)
            {
                case CIntervalEventType.EtInitialize:
                case CIntervalEventType.EtReverseInitialize:
                    m.Initialize(t);
                    break;
                case CIntervalEventType.EtInstant:
                case CIntervalEventType.EtReverseInstant:
                    m.Initialize(t);
                    m.Complete();
                    break;
                case CIntervalEventType.EtStep:
                    m.Step(t);
                    break;
                case CIntervalEventType.EtFinalize:
                case CIntervalEventType.EtReverseFinalize:
                    m.Complete();
                    break;
                case CIntervalEventType.EtInterrupt:
                    m.Interrupt();
                    break;
            }
        }
    }
}
