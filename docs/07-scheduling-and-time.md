# 07 — Scheduling & Time (`Panda3D.Framework.Scheduling`)

**Purpose.** The gameplay-facing scheduling and timing seams, distinct from the host pump. `IFrameScheduler` replaces `taskMgr` for application work with explicit ordering and disposable handles; `IGameClock` wraps the global clock; and this project owns clock configuration (the ticking chain and pacing) the host loop relies on.

**Replaces in `direct`.** `taskMgr.add` / `doMethodLater` / named tasks, and `globalClock`.

**Dependencies.** `Abstractions`; the fork's C# bindings — `Panda3d.Async` (the task manager / chains drive the scheduler and coroutines) and the `ClockObject` binding.

**Public surface.**
```csharp
public interface IGameClock { double Dt { get; } double FrameTime { get; } double RealTime { get; } long FrameCount { get; } }

public interface IFrameScheduler {
    IScheduledTask AddFrameTask(Func<FrameContext, TaskResult> task, int sort = FrameSlots.Gameplay, string? name = null);
    IScheduledTask AddTimed(TimeSpan delay, Action action);   // doMethodLater
    IScheduledTask AddFixedStep(double hz, Action<double> step, int sort = FrameSlots.Gameplay); // accumulator; step(dt) at fixed rate
    PandaTask DelayFrames(int n);                              // coroutine-style yield
}
public interface IScheduledTask : IDisposable { string? Name { get; } int Sort { get; } }

public static class SchedulingServiceCollectionExtensions {
    public static IServiceCollection AddClock(this IServiceCollection s, Action<ClockOptions>? o = null); // ticking chain + pacing
    public static IServiceCollection AddScheduler(this IServiceCollection s);
}
public sealed class ClockOptions {
    public bool LimitFrameRate { get; set; }      // -> ClockObject mode M_limited
    public double MaxFps { get; set; } = 60;       // -> SetFrameRate
    public double MaxDt { get; set; }              // -> SetMaxDt; caps the dt spike on a long frame (0 = off)
    public bool TickClock { get; set; } = true;    // enable default-chain ticking when no render tick source exists
}
```
`AddFrameTask`/`FrameContext`/`TaskResult`/`FrameSlots` are the same types the host uses for `AddHostedTask` ([02](02-hosting.md), [01](01-abstractions.md)); scheduler tasks and host tasks therefore share one ordering space and one task manager (see Design notes).

**Design notes.**
- **Explicit sort, disposable handles.** Unlike `taskMgr` (global, implicit ordering, name-based removal), tasks carry an explicit `sort` and return an `IScheduledTask` you dispose to remove — no global lookup, no name collisions. The `sort` lives on the same scale as the host's `dataLoop`/`igLoop` so gameplay tasks can be placed deterministically before or after render.
- **Two scheduling idioms coexist.** Classic per-frame callbacks (`AddFrameTask`) for batch-style updates (and the future ECS-ish shape), and coroutines (`await scheduler.DelayFrames(n)` / the `PandaTask` model) for sequential logic. Both are driven by the same `Poll()`.
- **Clock-tick ownership is explicit.** The global clock advances once per epoch from exactly one source. In rendered builds, `RenderFrame()` advances the clock; Rendering registers an `IClockTickSource` marker, and `AddClock` leaves the default chain's `tick_clock` off to avoid double-advance. In headless builds, no render tick source is registered, so `AddClock` enables `AsyncTaskChain.set_tick_clock(true)` on the `"default"` chain. If you ever need a custom tick source, disable `TickClock` or register an `IClockTickSource`; just ensure exactly **one** source.
- **Pacing (confirmed available).** `ClockObject`'s `M_limited` mode plus `SetFrameRate` are `PUBLISHED`; `SetMaxDt` caps the dt spike on a long frame. Setting `LimitFrameRate` puts the clock in `M_limited` at `MaxFps` (or register a sleep task) so an epoch can't free-run — the fix for the headless/vsync-off spin described in [02](02-hosting.md) and [00](00-overview.md) §5.
- **`IGameClock` is injected, never global.** Gameplay reads `Dt` from the injected clock — the structural replacement for ambient `globalClock.dt`, and the seam that makes fixed-step/deterministic testing possible.
- **Fixed-step is an accumulator, not a separate construct.** `AddFixedStep(hz, step)` is a frame task holding a time accumulator: each frame it adds `Dt` and calls `step(fixedDt)` zero or more times to drain whole fixed intervals, so physics/network ticks advance deterministically regardless of frame rate (the standard fixed-timestep pattern). It's the seam [Physics & Collision](11-physics-collision.md) uses for server-authoritative stepping. Nothing host-level — just a task at a chosen sort.
- **Scheduler tasks are native task-manager tasks.** `IFrameScheduler` materializes `AddFrameTask`/`AddFixedStep` as native `ManagedAsyncTask`s on the `"default"` chain — the *same* mechanism the host's `HostedTaskRunner` uses for `AddHostedTask` ([02](02-hosting.md)). So an app's gameplay tasks and the host's framework tasks live in one ordering space sorted by `FrameSlots`, and a gameplay task can be placed deterministically before/after `dataLoop`/`igLoop`. The difference is only ergonomic: `IFrameScheduler` is the gameplay-facing, `IGameClock`-aware API; `AddHostedTask` is the composition-root registration.

**Open items.**
- (none)

> **Verified:** `HostingLoopTests.RunsBootstrapAdvancesClockAndStops` covers headless chain ticking. `RenderingIntegrationTests.OffscreenViewRendersAndClockAdvancesViaRenderFrame` covers rendered/offscreen ticking and verifies `AddClock` does not double-tick because Rendering registers `IClockTickSource`.

> **Verified (1.11 headers):** `ClockObject` exposes `set_mode`/`get_mode` (with `M_limited` in the `Mode` enum), `set_frame_rate`, `set_max_dt`, `set_dt`, `set_frame_time`, `get_dt`, and `tick()` — all `PUBLISHED`. `AsyncTaskChain.set_tick_clock`/`get_tick_clock` are `PUBLISHED`. So pacing and explicit ticking are fully controllable from C#.

**See also.** [02 Hosting](02-hosting.md) (the loop, sort scale, ordering); [03 Rendering](03-rendering.md) (`igLoop`); [00 Overview](00-overview.md) §5–6 (frame, headless pacing).
