# Panda3D.Framework.Events

The C#↔Panda event seam. Panda's C++ side throws named events onto a global `EventQueue`; this package owns the single per-frame pump that drains that queue and routes events by name — the role Python `direct`'s `messenger`/`EventManager` played. Object-specific notifications ride this pump but surface as `System.Reactive` `IObservable<T>` on the objects that raise them (`view.Resized`, `devices.Connected`, …), so subscriptions are properly scoped and dispose deterministically.

## Provides

- `AddEvents()` — registers the single event pump and its per-frame drain task (`eventManager`, sort `FrameSlots.Events`). Idempotent: calling twice does not create a second drainer (the single-pump invariant).
- `INamedEventBus` — the raw name→subscriber routing for dynamic, string-identified events: `Observe(name)`, `Subscribe(name, handler)`, `Send(name, params object[])`.
- `NamedEvent` — a drained event parsed into `Name` + boxed `Parameters`, with `Get<T>(index)` / `TryGet<T>(index, out …)`.

There is no framework typed pub/sub bus — object notifications use each object's own `IObservable<T>`, and decoupled broadcast is bring-your-own (MessagePipe, a plain `Subject<T>`, …).

## Usage

```csharp
services.AddEvents();

// dynamic string events (Panda usually raises these; Send is the rare C#-side case):
using var sub = bus.Subscribe("my-event", e => Console.WriteLine(e.Get<int>(0)));
bus.Send("my-event", 42);
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
