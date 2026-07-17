# 02 — Hosting (`Panda3D.Framework.Hosting`)

**Purpose.** Owns the application lifecycle and the frame loop. Provides the ASP.NET-style `GameApplication` (`builder → Build → Run`) and the mechanism by which rendering, input, and gameplay are composed into a frame. This is where the central loop decision lives.

**Replaces in `direct`.** `ShowBase.run()` and the implicit "ShowBase is your app base class" model. Here the app is composed, not subclassed.

**Dependencies.** `Abstractions`; `Microsoft.Extensions.Hosting` (+ DI, Logging, Configuration); the bindings (`Panda3d.Async` task manager / `PandaTask`). It does **not** reference `Rendering`/`Input`/etc. — those are pulled in by the app's `Program.cs`. Hosting also provides the trivial `ISceneManager` implementation (a `render` root + a name→`NodePath` registry of get-or-create roots) via `AddSceneManager` — it's pure managed `NodePath` wrangling with no engine subsystem, so it sits naturally here rather than in its own assembly.

**Public surface.**
```csharp
public sealed class GameApplication {
    public static IGameApplicationBuilder CreateBuilder(string[] args);
    public IServiceProvider Services { get; }
    public void Run();   // builds nothing further; starts hosted tasks, spawns bootstrap, pumps Poll() until shutdown
}
public interface IGameApplicationBuilder {
    IServiceCollection Services { get; }
    IConfigurationManager Configuration { get; }
    GameApplication Build();
}
public interface IBootstrap { PandaTask RunAsync(); }   // the app's entry coroutine (spawned at gameplay slot)

// The seam any project uses to add native sorted tasks to the default chain.
// Sorts use the FrameSlots constants (01): DataLoop −50, Events −1, Gameplay 0,
// Intervals 20, Collision 30, Render 50, Audio 60.
public static class HostedTaskExtensions {
    public static IServiceCollection AddHostedTask(
        this IServiceCollection s, string name, int sort /* e.g. FrameSlots.Render */,
        Func<FrameContext, TaskResult> run);   // FrameContext (01): Services + Dt + Stopping — same signature as IFrameScheduler (07)
    public static IServiceCollection AddBootstrap<T>(this IServiceCollection s) where T : class, IBootstrap;
    public static IServiceCollection AddSceneManager(this IServiceCollection s);   // registers ISceneManager (the world-root service)
}
```

**The loop — core pump is `Poll()` only.**
```csharp
public void Run()
{
    // Blocking on purpose: awaiting on the main thread before the pump installs the
    // sync context would resume off-thread and break window/GL-context affinity.
    _host.StartAsync().GetAwaiter().GetResult();   // HostedTaskRunner materializes all hosted tasks here

    var sp    = _host.Services;
    var tasks = sp.GetRequiredService<IAsyncTaskManager>();
    var life  = sp.GetRequiredService<IHostApplicationLifetime>();

    // Frame supplied entirely by sorted tasks on the "default" chain (FrameSlots):
    //   −50 dataLoop   — Input
    //    −1 eventManager — queued Panda events -> C# observables
    //     0 gameplay   — coroutine resumption (dispatcher, pinned here) + user frame tasks
    //    20 ivalLoop   — Intervals      30 collisionLoop — Collision
    //    50 igLoop     — Rendering [omitted headless]      60 audioLoop — Audio
    //   −51 resetPrevTransform — Collision (fluid motion; see 11)
    PandaTask.Spawn(() => sp.GetRequiredService<IBootstrap>().RunAsync());

    var stopping = life.ApplicationStopping;
    while (!stopping.IsCancellationRequested)
        tasks.Poll();                       // the entire core loop

    _host.StopAsync().GetAwaiter().GetResult();       // HostedTaskRunner removes the native tasks here
    _host.Dispose();
}
```

Rationale: this mirrors how ShowBase actually works — `base.run()` is `while True: taskMgr.step()`, with render and input as sorted tasks (`igLoop`/`dataLoop`), and the sort table above is `direct`'s own (see 00 §5). Making the task manager the loop means ordering ports by sort number, render becomes movable (half-rate, RTT pre-pass, multiple outputs), and headless is "don't register rendering."

**HostedTaskRunner (how `AddHostedTask` works).** `AddHostedTask` only records a descriptor (name, sort, delegate) in DI. A single internal `IHostedService` — the `HostedTaskRunner` — materializes every descriptor into a native `ManagedAsyncTask` on the default chain during `StartAsync` (so native tasks exist only while the host runs) and removes them during `StopAsync`. Task delegates receive a `FrameContext` ([01](01-abstractions.md)): the root `IServiceProvider`, this frame's `Dt`, and the host's stopping token — the same shape `IFrameScheduler` uses, so the two registration paths are interchangeable. `AddBootstrap<T>` registers the app's `IBootstrap`; `Run` spawns it as a coroutine (gameplay slot) right after the host starts.

**Two idioms, one frame.** Sequential gameplay logic lives in **coroutines** (`PandaTask.Spawn` + `await NextFrame()`), resuming in the gameplay slot (0) — exactly where `direct`'s `taskMgr.add(..., sort=0)` tasks run. Recurring per-frame work that isn't sequential lives in **frame tasks** (`AddHostedTask` at a chosen `FrameSlots` value, or the gameplay-facing `IFrameScheduler` in [07](07-scheduling-and-time.md)). Both are native task-manager tasks ordered by sort — there is no separate pipeline layer; the task manager *is* the pipeline, as in `direct`.

**The four concerns this design forces.**

1. **Pacing.** While rendering is registered with vsync on, the `igLoop` task blocks on the buffer flip and paces the loop. Headless or vsync-off, `Poll()` returns immediately and pegs a core. Mitigation (as Panda does): global clock in limited mode with a frame-rate cap, or a sleep task. Designed in because the server build is the spin-prone case.

2. **Render is a native sorted task, not a coroutine.** With the dispatcher pinned at `FrameSlots.Gameplay` and `igLoop` at `FrameSlots.Render`, "gameplay updates, then render" is simply the sort table. The remaining rule: render must be a real native sorted task — **not** a `while { RenderFrame(); await NextFrame(); }` coroutine, which would resume *inside* the gameplay slot and lose its place in the table.

3. **Clock advances once per epoch from exactly one source.** In rendered builds, `RenderFrame()` advances the global clock, so Rendering registers an `IClockTickSource` marker and `AddClock` does not enable chain ticking. In headless builds, no render tick source is registered, so `AddClock` enables `tick_clock` on the default `AsyncTaskChain`. Ensure exactly one tick source; details and the verified clock surface are in [07](07-scheduling-and-time.md).

4. **Shutdown and errors.** Two shutdown sources converge on `ApplicationStopping`: the console lifetime (Ctrl-C/SIGTERM) and a small hosted task — registered by Rendering, since it owns the window — that calls `life.StopApplication()` when `window.IsClosed()` (or via the view's `Closed` observable, see [03](03-rendering.md)). Task exceptions flow through the task error path (not a `try/catch` around `RenderFrame`); that path must escalate to shutdown, or a dead context spins silently.

**Coroutines and async.** Gameplay coroutines are `PandaTask.Spawn(...)` + `await PandaTask.NextFrame()`, resumed by `Poll()`. Loader requests (and any other Panda async result) are directly awaitable, so asset loading is plain `await`-able over the native `Loader`'s requests; how that awaiting works is a `Panda3D.Async`/binding concern, not a framework one.

**Fixed-step work.** Deterministic fixed-rate updates (physics, network ticks) use a small accumulator inside a frame task — exposed as `IFrameScheduler.AddFixedStep(hz, step)` in [07](07-scheduling-and-time.md) — not a dedicated host construct. The v2 ECS schedule is likewise just a frame task that calls `group.Update(dt)` at a documented sort.

**Open items.**
1. **Pin the dispatcher sort.** `direct` has no drain task — a Python coroutine *is* a task and resumes at its own sort (default 0). `Panda3D.Async` funnels C# continuations through a per-chain dispatcher task instead, so the equivalent guarantee is making the dispatcher's task sort configurable and pinning it to `FrameSlots.Gameplay` (0). This is a binding-side change on the fork.
2. Confirm window creation + pump on the entry thread for target platforms (macOS thread-0 in particular). The samples already do this on the entry thread.

**See also.** [00 Overview](00-overview.md) §5 (the frame end to end, sort table); [01 Abstractions](01-abstractions.md) (`FrameSlots`); [03 Rendering](03-rendering.md) (`igLoop`, window-close task); [05 Input](05-input.md) (`dataLoop`); [07 Scheduling & Time](07-scheduling-and-time.md) (clock/pacing).
