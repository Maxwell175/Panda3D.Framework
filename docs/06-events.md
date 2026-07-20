# 06 — Events (`Panda3D.Framework.Events`)

**Purpose.** The C#↔Panda event seam. Panda's C++ side throws named events (`throw_event(name, params…)`) onto a global `EventQueue`; this project owns the per-frame **pump** that drains that queue and routes events by name to C# subscribers — the role Python's `messenger`/`EventManager` plays. On top of that one mechanism, objects expose their notifications as **`System.Reactive` `IObservable<T>`** (so subscriptions are properly scoped and compose with the Rx ecosystem), and a small **`INamedEventBus`** exposes the raw name→subscriber routing for genuinely dynamic, string-identified events. The framework does **not** ship its own typed pub/sub bus — the ecosystem already has those.

**Replaces in `direct`.** Python's `EventManager` (the C++-queue→`messenger` pump) and the `messenger`/`DirectObject.accept` model. Critically it fixes the messenger's leak class — the messenger held strong refs to acceptors, so objects had to manually `ignoreAll()`/`destroy()` ("missiles still fire after delete") — by making every subscription an `IDisposable` (Rx's own contract). Maintainer issue #1636 agrees the Python event layer *"can be made redundant with minor improvements to the C++ EventHandler and throw_event interfaces."*

**Dependencies.** `Abstractions`; the fork's C# bindings for `EventQueue`/`Event`/`EventHandler`. `System.Reactive` is **not** required by this project's core surface — `IObservable<T>` is built into .NET; only implementations and consumers that want Rx operators reference `System.Reactive` (see 01).

## How events get from Panda to C# (the pump)

Verified against Python's `EventManager.doEvents()`, which is exactly this loop:
```
each frame (eventManager task, FrameSlots.Events = −1):
    while !EventQueue.GetGlobalEventQueue().IsQueueEmpty():
        e = EventQueue.GetGlobalEventQueue().DequeueEvent()
        route e.Name → subscribers   (and EventHandler.dispatch_event(e) so any C++ hooks still fire)
```
All of `EventQueue.GetGlobalEventQueue()`, `IsQueueEmpty`, `DequeueEvent`, `Event.Name`/`Parameters` are `PUBLISHED`, so this needs **no fork change** — it is the same mechanism Python has shipped for 20 years. The pump is the C# equivalent of Python's `EventManager`, and `INamedEventBus` is its routing table (the `messenger` equivalent).

**Single-pump invariant.** `DequeueEvent()` removes events from the *one* global queue, so there must be exactly **one** pump draining it (as `direct` has exactly one `EventManager`). All C# event reception — the named bus and every object observable built on it — flows through this single pump. Two drainers would each see half the events.

**Public surface.**
```csharp
// The raw name→subscriber routing (the "messenger" equivalent). For dynamic, string-identified events.
public interface INamedEventBus {
    IObservable<NamedEvent> Observe(string name);            // events with this name, as an observable
    IDisposable Subscribe(string name, Action<NamedEvent> handler);  // convenience over Observe(...).Subscribe(...)
    void Send(string name, params object[] parameters);      // throw_event(name, …) — rare; mostly Panda raises these
}
public readonly record struct NamedEvent(string Name, IReadOnlyList<object> Parameters) {  // a drained Event, parsed (not System.EventArgs)
    public int Count { get; }                                // Parameters.Count
    public T Get<T>(int index);                              // typed access; throws InvalidCastException if the param isn't T
    public bool TryGet<T>(int index, out T value);           // typed access; false if the index is absent or the wrong type
}

public static class EventsServiceCollectionExtensions {
    public static IServiceCollection AddEvents(this IServiceCollection s);   // registers the pump task + INamedEventBus
}
```

That is the *entire* public surface. There is no `IEventBus`, no `Subscriptions` bag, no `IEngineEventBridge` — see Design notes.

## Object notifications are `IObservable<T>`

A specific object's notifications live **on that object** as `IObservable<T>` properties, not on a central bus:
- `IView.Resized : IObservable<WindowSize>`, `IView.Closed`, `IView.FocusChanged`, `IView.Minimized` ([03](03-rendering.md))
- `IDevices.Connected : IObservable<InputDevice>`, `Disconnected` ([05](05-input.md))
- collision streams ([11](11-physics-collision.md)), widget streams ([10](10-gui.md))

Each is built privately by the owning project from the pump: subscribe to the relevant Panda event name via `INamedEventBus`, project the parameters into a typed value, and expose it as an observable (a `Subject<T>` the implementation pushes, or `INamedEventBus.Observe(name).Select(...)`). The observable's lifetime **is the owning object's lifetime** — it completes/disposes when the object does, which is the scoping the central-bus approach lacked. Consumers subscribe with standard Rx (`view.Resized.Where(...).Subscribe(...)`), get an `IDisposable` back, and compose cleanup with Rx's `CompositeDisposable`/`DisposeWith`.

**Per-source disambiguation (windows).** Rather than every view filtering a shared `"window-event"`, each view gives *its* window a unique event name via `GraphicsWindow.set_window_event($"window-event-{id}")` (`PUBLISHED`) and observes only that — so a resize on one window doesn't wake the others. The window-event parameter *is* the `GraphicsWindow` (verified: `throw_event(_window_event, this)`), so filtering by window is also possible where a shared name is preferred.

**Design notes.**
- **No framework typed bus.** For *decoupled broadcast* (`PlayerDied`, `WaveStarted` — publisher and subscriber don't know each other), the ecosystem already has good options (MessagePipe, a plain `Subject<T>`, etc.); the framework doesn't ship a competing one. Object-specific notifications use the object's own `IObservable<T>`; cross-cutting broadcast is bring-your-own. This is the minimally-opinionated stance (same as deferring ECS).
- **No `IEngineEventBridge`.** Turning a Panda string event into a typed observable is a one-liner each project does privately (`INamedEventBus.Observe(name).Select(project)` or a pushed `Subject`), not a shared registry API. The "bridge" is just this pattern plus the pump.
- **Lifetime is Rx-standard.** Subscribing returns `IDisposable`; an object that owns several collects them in an Rx `CompositeDisposable` and disposes it (or ties it to its DI scope). No `SubscribeWeak` — weak handlers fail silently when GC'd; deterministic disposal is clearer. (We use Rx's disposable plumbing rather than a home-grown bag.)
- **Delivery is synchronous on the pump's chain.** The pump drains and dispatches in-line on its task's chain each frame at `FrameSlots.Events` (−1), after `dataLoop` and before default gameplay tasks/coroutines read object observables. Handlers run on the `"default"` chain (safe for scene-graph mutation). For off-thread sources, marshal back before the value reaches the queue, or use Rx `ObserveOn` to schedule. Re-entrancy (a handler that mutates the scene mid-collision-traversal) is handled by the consumer with Rx (`ObserveOn` a frame-boundary scheduler), not a framework knob.
- **`Send` is rarely used.** Almost all named events originate from Panda (`throw_event` in C++). `INamedEventBus.Send` exists for the data-driven case where C# wants to raise a dynamic string event, but typed observables are the norm.

**Non-features (v1).** No typed pub/sub bus (bring your own), no ordered/prioritized handler dispatch (Rx operators or context priority in [Input](05-input.md) cover the real cases), no weak subscriptions.

**Open items.**
- (none)

> **Verified:** The pump task runs at `FrameSlots.Events` (−1). `HostingLoopTests.EventPumpRunsBeforeDefaultGameplayTasks` queues an event in `dataLoop` and proves a default gameplay task observes it in the same epoch. `EventPumpTests` cover numeric/string parameters and native object payloads (`PandaNode` through `EventParameter` → `INativeObject` → `CastTo<T>()`), matching the payload shape used by windows, devices, and collisions.

**See also.** [02 Hosting](02-hosting.md) (the pump runs as a frame task); [01 Abstractions](01-abstractions.md) (`IObservable<T>` as the notification primitive; no new dependency); consumers: [03 Rendering](03-rendering.md) (window observables), [05 Input](05-input.md) (device observables), [11 Physics & Collision](11-physics-collision.md), [10 GUI](10-gui.md).
