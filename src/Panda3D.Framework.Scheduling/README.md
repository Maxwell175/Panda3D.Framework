# Panda3D.Framework.Scheduling

The gameplay-facing scheduling and timing seams, distinct from the host pump. `IFrameScheduler` replaces `taskMgr` with explicit sort ordering and disposable handles; `IGameClock` wraps the engine's global clock; and this package owns clock configuration (pacing and which source ticks the clock) that the host loop relies on. Replaces `direct`'s `taskMgr` / `doMethodLater` and `globalClock`.

## Provides

- `AddScheduler()` — registers `IFrameScheduler` (and an `IGameClock` for it to read).
- `AddClock(o => …)` — configures clock pacing and ticking via `ClockOptions`, and registers `IGameClock`.
- `IFrameScheduler` — `AddFrameTask`, `AddTimed` (doMethodLater), `AddFixedStep(hz, step)` (deterministic fixed-timestep accumulator), `DelayFrames(n)`; each registration returns a disposable `IScheduledTask`.
- `IGameClock` — read-only `Dt` / `FrameTime` / `RealTime` / `FrameCount` (the contract lives in Abstractions; the working implementation is here).
- `ClockOptions` — `LimitFrameRate` / `MaxFps` / `MaxDt` / `TickClock`.
- `AddFrameTask(name, sort, …)` (on `IServiceCollection`) — setup-time native sorted-task registration bracketed to the host lifetime.
- `PandaFrameTask` — the low-level primitive: a managed callback registered as a native sorted task on a chain at an explicit sort (advanced).

```csharp
builder.Services.AddClock(o => { o.LimitFrameRate = true; o.MaxFps = 60; });
builder.Services.AddScheduler();

// later, from a coroutine or resolved service:
var scheduler = app.Services.GetRequiredService<IFrameScheduler>();
using var tick = scheduler.AddFrameTask(ctx =>
{
    Advance(ctx.Dt);
    return TaskResult.Continue;
}, sort: FrameSlots.Gameplay);   // dispose the handle to remove the task
```

The global clock advances once per epoch from exactly one source: `RenderFrame` in rendered builds (Rendering registers an `IClockTickSource`), or the `"default"` task chain headlessly (`ClockOptions.TickClock`, on by default). `AddClock` won't enable chain ticking when a tick source is already present, avoiding a double-advance. Scheduler tasks and the host's setup-time tasks are the same native task-manager tasks, so they share one `FrameSlots` ordering space.

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
