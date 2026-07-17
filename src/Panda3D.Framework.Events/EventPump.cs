using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Panda3D.Core;

namespace Panda3D.Framework.Events;

/// <summary>
/// The single pump that drains Panda's global C++ <c>EventQueue</c> each frame and routes events by
/// name to C# subscribers. The one drainer of the global queue (the single-pump invariant): all C#
/// event reception flows through here. Subscribers run in-line during <see cref="PumpOnce"/>, safe for
/// scene-graph mutation.
/// </summary>
internal sealed class EventPump : INamedEventBus, IDisposable
{
    readonly ConcurrentDictionary<string, Subject<NamedEvent>> _subjects = new();
    int _disposed;

    /// <summary>Drain and dispatch every event currently queued. Called once per frame.</summary>
    public void PumpOnce()
    {
        if (_disposed != 0) return;

        var queue = EventQueue.GetGlobalEventQueue();
        var handler = Panda3D.Core.EventHandler.GetGlobalEventHandler();

        while (!queue.IsQueueEmpty())
        {
            var e = queue.DequeueEvent();
            if (e is null) continue;

            var name = e.GetName();
            if (!string.IsNullOrEmpty(name) && _subjects.TryGetValue(name, out var subject))
            {
                var parsed = new NamedEvent(name, Parse(e));
                subject.OnNext(parsed);
            }

            // still dispatch to the C++ handler so native hooks fire
            handler.DispatchEvent(e);
        }
    }

    public IObservable<NamedEvent> Observe(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return GetOrCreate(name);
    }

    public IDisposable Subscribe(string name, Action<NamedEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return GetOrCreate(name).Subscribe(handler);
    }

    public void Send(string name, params object[] parameters)
    {
        ArgumentNullException.ThrowIfNull(name);

        var e = new Event(name);
        if (parameters is not null)
        {
            foreach (var p in parameters)
                e.AddParameter(ToParameter(p));
        }
        // queue globally so it flows through this same pump
        EventQueue.GetGlobalEventQueue().QueueEvent(e);
    }

    Subject<NamedEvent> GetOrCreate(string name) =>
        _subjects.GetOrAdd(name, static _ => new Subject<NamedEvent>());

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (var s in _subjects.Values)
            s.OnCompleted();
        _subjects.Clear();
    }

    // ---- EventParameter <-> object ----

    static IReadOnlyList<object> Parse(Event e)
    {
        var pars = e.Parameters;
        int n = pars.Count;
        if (n == 0) return Array.Empty<object>();

        var list = new object[n];
        for (int i = 0; i < n; i++)
            list[i] = ParseOne(pars[i]);
        return list;
    }

    static object ParseOne(EventParameter p)
    {
        if (p.IsInt()) return p.GetIntValue();
        if (p.IsDouble()) return p.GetDoubleValue();
        if (p.IsString()) return p.GetStringValue();
        if (p.IsWstring()) return p.GetWstringValue();
        if (p.IsTypedRefCount())
            return p.GetTypedRefCountValue() ?? (object)EmptyParameter.Instance;

        // pointer payloads (CollisionEntry, PandaNode, …) are TypedWritableReferenceCount, so
        // IsTypedRefCount is false; hand back the native object for the caller to CastTo<>
        var ptr = p.GetPtr();
        if (ptr is not null) return ptr;

        return EmptyParameter.Instance;
    }

    static EventParameter ToParameter(object value) => value switch
    {
        null => new EventParameter(),
        int i => new EventParameter(i),
        double d => new EventParameter(d),
        float f => new EventParameter((double)f),
        long l => new EventParameter((int)l),
        string s => new EventParameter(s),
        ITypedReferenceCount trc => new EventParameter(trc),
        ITypedWritableReferenceCount twrc => new EventParameter(twrc),
        _ => new EventParameter(value.ToString() ?? string.Empty),
    };

    /// <summary>Placeholder for an empty/unsupported parameter, so consumers never see a bare null.</summary>
    public sealed class EmptyParameter
    {
        public static readonly EmptyParameter Instance = new();
        EmptyParameter() { }
        public override string ToString() => "<empty>";
    }
}
