# 00 — Architecture Overview

> **Status:** design of record, top level. The earlier single design doc is now split: this is the overview; one sub-document per project follows (see the index at the end). The package family is `Panda3D.Framework.*`; the `Panda3D.Framework` umbrella package is the one-reference entry point that pulls in every feature library, the C# bindings, and all four native runtimes.
>
> **Background references:** the subsystem-by-subsystem mapping of Python `direct` and the DI→ECS path live in the first research report; the Panda3D core/rendering object model and per-window scoping live in the second. This document and its siblings are the *design*; those two are the *research* behind it.

---

## 1. Architecture in one page

The library replaces Python `direct` (ShowBase, taskMgr, messenger, intervals, Actor, GUI, …) with C# building blocks composed by **dependency injection**. It runs on the `Maxwell175/panda3d` fork (`csharp` branch, Panda3D 1.11 — the engine + C# bindings, including `CSHARP_EXTENSION` members), consumed as the **`Panda3D.Interop`** binding package (namespace `Panda3D.Core`) with **`Panda3D.Async`** providing the async/coroutine layer.

**The guiding goal:** combine **maximum flexibility** — even very complex setups (multiple windows over one shared scene, multiple independent scene roots à la the two screens of a DS, per-zone MMO server simulation, headless builds) should be natural, not fought for — with **simple defaults** that let someone pick the library up and start prototyping in a few lines. Every subsystem should have an easy `AddXxx()` path *and* expose the explicit seams underneath it.

**The recommended entry point** is the `Panda3D.Framework` umbrella package: one reference pulls in every feature library, the C# bindings, and all four native runtimes (`linux-x64`, `osx-x64`, `osx-arm64`, `win-x64`). On top of the per-subsystem `AddXxx` seams it ships two conveniences — `AddGame(Action<ViewOptions>?)`, which wires scene + events + clock + scheduler + rendering + a window + input in a single call, and `AddViewBootstrap(...)`, which registers the entry coroutine in the *main view's* scope so per-view services (`IInput`, `IGui`) inject directly. The minimal `SpinningCard` sample is the whole shape: `AddGame`, `AddViewBootstrap`, `builder.Build().Run()`.

Five decisions shape everything:

- **DI-first, container-agnostic.** The core depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`; MS.DI is the default, DryIoc/Autofac swap in at the composition root. The library never calls `BuildServiceProvider()` and never relies on container-specific behavior.
- **ASP.NET-style host.** `builder → Build → Run`, giving configuration, logging, options, `IHostApplicationLifetime`, and `IHostedService` for free. `Run()` keeps a frame pump alive on the main thread (the one ASP.NET-vs-game divergence).
- **The task manager is the loop.** The core pump does only `taskManager.Poll()`. Rendering and input are **registered native sorted tasks** (`igLoop` high sort → `RenderFrame()`; `dataLoop` low sort → data-graph traverse), exactly as ShowBase structures `base.run()`. Anything that must run before/after render orders by sort number; headless is "don't register rendering."
- **Three scoping tiers.** *Engine-wide singletons* (engine, pipe, clock, task manager, loader, the pump); *shared world scope* (`render` `NodePath` root(s), models, textures, GSG); *per-output/per-viewer scope* (window, display regions, camera+lens, 2-D overlay, input chain). Because a scene root is just a registered `NodePath`, both "two windows, one shared scene" **and** "two windows, two independent scene roots" (the DS-two-screens shape) are registration choices, as is per-zone MMO simulation.
- **ECS-ready, ECS-free in v1 — by discipline, not abstraction.** The framework ships no world/entity type of its own (there are mature .NET ECS libraries; a pass-through would add nothing, and any real ECS work is v2). Readiness is kept for free by keeping per-entity state POCO-separable from logic; a developer can bring their own ECS and run it as a frame task.

No ShowBase antipatterns survive: no god-object, no global `base`/builtins, no implicit singletons, no stringly-typed-only global messenger, no magic attribute side-effects, no implicit task sort keys scattered across modules.

---

## 2. Project map

| Project | Replaces in `direct` | Key public types | Doc |
|---|---|---|---|
| **`Panda3D.Framework`** *(umbrella meta-package — the recommended entry point)* | — | `AddGame(Action<ViewOptions>?)`, `AddViewBootstrap(Func<IServiceProvider,PandaTask>` / injected `Delegate)`; pulls in every feature library, the bindings, and all four native runtimes | this doc |
| `…Abstractions` | (contracts for all of the below) | the `IXxx` interfaces, `XxxOptions`, `AddXxx` signatures | [01](01-abstractions.md) |
| `…Hosting` | `ShowBase.run`, the main loop | `GameApplication` (`CreateBuilder`/`Run`), `AddBootstrap` (three overloads: class `<T>`, `Func<IServiceProvider,PandaTask>`, injected `Delegate`), `AddSceneManager` (impl of `ISceneManager`) | [02](02-hosting.md) |
| `…Rendering` | ShowBase window/pipe/camera setup, `igLoop` | `IView`, `IViewManager`, `ICameraRig`, `AddEngine/Window/Rendering` | [03](03-rendering.md) |
| `…Input` | ShowBase data graph, `dataLoop`, key events | `IInput`, `IInputContext`, `IDevices`, `AddInput` | [05](05-input.md) |
| `…Events` | `messenger` + `DirectObject.accept` | queue-drain pump, `INamedEventBus`; `IObservable<T>` on objects | [06](06-events.md) |
| `…Scheduling` | `taskMgr` (gameplay), `globalClock` | `IFrameScheduler`, `IScheduledTask`, `IGameClock` | [07](07-scheduling-and-time.md) |
| `…Intervals` | `direct.interval` (Lerp/Sequence/Parallel/Func) | `Sequence`, `Parallel`, `Lerp<T>`, `IIntervalManager` over C++ `CInterval` | [08](08-intervals.md) |
| `…Actors` | `direct.actor.Actor`, `ActorInterval` | `IActor`, `IActorLoader`, `ActorInterval`, `CrossFade` over native `AnimControl`/`PartBundle` | [09](09-actors-animation.md) |
| `…Gui` | `direct.gui.DirectGui` | `IGui`, `Widget` classes (`Button`/`Entry`/`Slider`/…) over PGui | [10](10-gui.md) |
| `…Physics` | `base.cTrav`, collision patterns | `ICollisionWorld`, `ICollisionQuery`, `RaycastHit` (observables of native `CollisionEntry`), `AddCollision`; Bullet used natively | [11](11-physics-collision.md) |
| `…Particles` | `enableParticles`, `ParticleEffect`, `ParticleInterval` | `IParticles`, `ParticleEffect`, `ParticleInterval`, `AddParticles` over native `ParticleSystemManager` | [11](11-physics-collision.md) |
| `…Audio` | ShowBase audio managers, `Audio3DManager` | `IAudio`, `IAudio3D` over native `AudioManager`/`AudioSound` | [12](12-audio-misc.md) |
| `…Build` *(build-time, MSBuild-only)* | manual `egg2bam`/`multify` runs, ad-hoc asset copy | items `PandaContent`, `PandaResource`+`PandaProcessor`, `PandaBundle`; no runtime dll | [04](04-resources.md) |

---

## 3. Dependency graph

Per-project references (framework projects only; every project also references `Abstractions`, and all may reference the bindings — the `Panda3D.Interop` package (namespace `Panda3D.Core`) + `Panda3D.Async` — which is not repeated below):

| Project | References (framework) | Notes |
|---|---|---|
| `Abstractions` | — | contracts only |
| `Events` | — | queue-drain pump + `INamedEventBus`; objects expose `IObservable<T>` |
| `Scheduling` | — | clock + scheduler |
| `Rendering` | `Events`, `Scheduling` | window observables demuxed from the per-window event via the pump; render task/tick-source marker |
| `Audio` | `Events`, `Scheduling` | update task at the audio slot; finished-events via the pump |
| `Input` | `Rendering`, `Events`, `Scheduling` | needs a `GraphicsWindow`; device observables via the pump; dataLoop task |
| `Intervals` | `Scheduling`, `Events` | steps C++ `CIntervalManager`; awaitable via done-event/pump |
| `Actors` | `Intervals` | playback/blending on native `AnimControl`/`PartBundle`; no animation events (engine raises none) |
| `Physics` | `Events`, `Scheduling` | collision traverse/query as frame tasks; collision observables (native `CollisionEntry` payloads) |
| `Particles` | `Scheduling`, `Intervals` | particle-system update as a frame task; `ParticleInterval` |
| `GUI` | `Rendering`, `Events` | widgets over PGui; observables via the pump; (+ audio optional, widget sounds) |
| `Hosting` | `Scheduling` | + `Microsoft.Extensions.Hosting`; the app's `Program.cs` composes concrete modules |
| `Panda3D.Framework` (umbrella) | *every feature library* | meta-package; also brings the native `Panda3D.Runtime.<rid>` packages (`linux-x64`/`osx-x64`/`osx-arm64`/`win-x64`) |

Layering, stated plainly: `Abstractions` is the base (contracts only). `Scheduling` and `Events` are the low-level runtime seams. Rendering, Audio, Input, Intervals, Actors, Physics, Particles, and GUI compose those seams as shown in the table (`ISceneManager` is a contract in `Abstractions`; its trivial implementation ships in `Hosting`). `Hosting` owns the loop; the app's `Program.cs` is the place that references concrete modules together. The graph is acyclic.

---

## 4. Cross-cutting rules every project honors

- **Container-agnostic.** Reference `…DependencyInjection.Abstractions` only; ship `AddXxx(this IServiceCollection, Action<TOptions>)` extensions; configure via `IOptions<T>`; no `BuildServiceProvider()`; don't depend on last-registration-wins (prefer keyed/explicit).
- **Lifetimes.** Singletons for engine-wide services; **scoped** for per-window/per-connection/per-zone state; transient for value-like helpers. Resolve scoped state from `IServiceScopeFactory`, never by constructor-injecting it into a singleton.
- **Thread affinity.** All scene-graph mutation runs on the loop / `"default"` chain. Offload pure compute via `await PandaTask.SwitchToChain("workers")` and `SwitchToChain("default")` before touching nodes. For asset loads after worker threads have started, prefer the async loader (loader requests are directly `await`-able); synchronous `LoadSync` is best kept to bootstrap.
- **Events over messenger.** A single pump drains Panda's C++ event queue (the `messenger`/`EventManager` role); objects expose their own notifications as `System.Reactive` `IObservable<T>` (`view.Resized`, `devices.Connected`, …), owned by the project that raises them. `INamedEventBus` handles genuinely dynamic string events. The framework ships no typed pub/sub bus — decoupled broadcast is bring-your-own (MessagePipe / `Subject<T>`). See [06](06-events.md).
- **Explicit ordering over implicit sort keys.** Per-frame order is expressed once, visibly (named task sorts on one shared scale), not as magic numbers spread across modules.
- **POCO data for the ECS future.** Per-entity state is plain data, separable from the services that act on it — so any ECS library can adopt it later without a framework-owned world type.

---

## 5. The frame, end to end

One epoch of `taskManager.Poll()` runs the default chain's tasks in sort order. We adopt **`direct`'s battle-tested sort convention** (from ShowBase) as the framework's named slots:

| sort | slot | owner |
|---|---|---|
| −51 | `resetPrevTransform` — fluid-motion bookkeeping (adopted: fluid lerps/pushers, 08/11) | Physics |
| −50 | `dataLoop` — data-graph traverse, input current | Input |
| −1 | `eventManager` — drain queued Panda events before gameplay reads observables | Events |
| **0** | **gameplay** — C# coroutine resumption + user frame tasks (default sort) | Hosting/Scheduling |
| 20 | `ivalLoop` — interval/tween stepping | Intervals |
| 30 | `collisionLoop` — explicit collision traverse | Physics |
| 50 | `igLoop` — `engine.RenderFrame()`, renders every output | Rendering |
| 60 | `audioLoop` — audio manager update | Audio |

(`direct` also runs state-cache GC at 46; we adopt that if/when needed. `resetPrevTransform` is adopted — see the −51 row.) In rendered builds, `igLoop`/`RenderFrame()` advances the global clock; `AddClock` detects that render tick source and does not also enable chain ticking. In headless builds, `AddClock` makes the default `AsyncTaskChain` tick the clock once per epoch instead (see [07](07-scheduling-and-time.md)).

**Coroutine resumption.** In `direct`, a coroutine *is* a task: awaiting a future re-activates that same task at **its own sort** (`AsyncFuture::wake_task`), so user coroutines deterministically resume at sort 0, before intervals/collision/render. `Panda3D.Async` instead drains C# continuations through a per-chain dispatcher task, so the equivalent guarantee requires **pinning the dispatcher's sort to the gameplay slot (~0)** — a binding-side knob, tracked in [02](02-hosting.md) open item #1.

Pacing comes from `RenderFrame()` + vsync blocking inside `igLoop`. **Headless (no `igLoop`) needs a pacing mechanism** (clock `M_limited` + frame-rate cap), or `Poll()` free-runs and pegs a core; the clock itself keeps ticking because `AddClock` enables chain ticking when no render tick source is registered.

---

## 6. Build/run shapes

**Client** registers engine + window + world + input + rendering (+ gameplay modules) and calls `app.Run()`, which pumps on the main thread. This is the `SpinningCard` and `RoamingRalph` shape: `AddGame` plus the feature modules the game needs (`AddActors`, `AddCollision`, …), then `builder.Build().Run()`. All three samples (`SpinningCard`, `RoamingRalph`, `RoamingRalphMultiplayer`) use top-level statements — the composition root is `Program.cs` itself, no `static Main`.

**Server** registers world (headless) + pacing + simulation, omits rendering/window/input, and calls `app.Run()` — the same pump, blocking until shutdown (Ctrl-C/SIGTERM via `ApplicationStopping`). With no graphics context there's nothing to render, so the loop just polls the simulation tasks; per-zone simulation can be ordinary hosted tasks/services matching per-zone scopes. The only difference between client and server is which modules are registered, not the entry shape. `RoamingRalphMultiplayer` demonstrates exactly this: one `Program.cs` whose `--bot` path registers scene + events + clock + scheduler + collision (no rendering/window/input) while the default path adds `AddGame` + `AddGui` + `AddActors` — same builder, same `Run()`.

---

## 7. Phasing

- **v1** — all projects above with DI, no ECS. Milestones:
  1. A sample game on the framework demonstrating the "simple default" path (a few `AddXxx` lines to a running prototype) — realized by `SpinningCard` (minimal, no assets) and `RoamingRalph`.
  2. A headless server build compiling against the same gameplay assembly — realized by `RoamingRalphMultiplayer`, whose `--bot` mode shares one `Program.cs` with the rendered client.
  3. **Multi-view acceptance:** a second window viewing the *same* scene via a second camera (shared GSG) works, **and** two windows each with an *independent* scene root (the DS-two-screens shape) works — both as registration changes only.
- **v2** — optional ECS guidance and thin glue (not a framework abstraction): a documented pattern for running a chosen ECS library's schedule as a frame task at a documented sort, and a `TransformSync` example bridging ECS transforms to the scene graph. Developers who don't want ECS are unaffected.

---

## 8. Non-goals (explicitly out of scope)

To keep the surface focused, the following `direct` components are deliberately **not** reimplemented — most are legacy, editor/tooling, or already better served by the .NET platform. Several are flagged by the maintainers themselves (issue #1636) as legacy or removable.

- **FSM / ClassicFSM** — deferred; the C# ecosystem (Stateless) covers transition logic, and only a thin per-frame `IState` ticker is game-specific. See [12](12-audio-misc.md).
- **DirectNotify** — use `Microsoft.Extensions.Logging` (`ILogger<T>`) instead.
- **DConfig / `base.config`** — use the .NET configuration/options pattern (the host's `IConfiguration` + `IOptions<T>`).
- **Editor/tooling: tkpanels, directtools, the level editor, particle panel, BufferViewer, `inspect()`** — out of scope; these are authoring tools, not runtime.
- **Tk/wx integration** — N/A; UI tooling is not a runtime concern here.
- **Cluster client/server, DirectD** — out of scope.
- **Cg-based `CommonFilters`/`Filter`** — the Cg path is deprecated upstream; post-processing, if needed, is a separate concern built on modern shaders.
- **Distributed networking (DC layer), SmoothMover** — out of scope for the core; a candidate for a separate optional package if needed, mirroring the maintainers' own modularization plan.

This list is about the *framework's* scope, not a judgment that these are useless — a consuming game can still use any Panda facility directly through the bindings.

---

## 9. Document index

- **00 — Architecture Overview** (this file)
- **[01 — Abstractions](01-abstractions.md)** — contracts, container-agnostic rules
- **[02 — Hosting](02-hosting.md)** — `GameApplication`, the Poll-only loop, sorted tasks, pacing/ordering/clock/shutdown
- **[03 — Rendering](03-rendering.md)** — engine/window/output/camera, `igLoop`, multi-window/shared-scene
- **[04 — Resource Pipeline](04-resources.md)** — build-time assets: blind copy / multifile packing / egg2bam + escape hatch, over `Panda3D.Tools` (build-only, no runtime mount code)
- **[05 — Input](05-input.md)** — data graph, `dataLoop`, pull-based input + action map
- **[06 — Events](06-events.md)** — the queue-drain pump replacing the messenger; object notifications as `IObservable<T>`; `INamedEventBus` for dynamic string events
- **[07 — Scheduling & Time](07-scheduling-and-time.md)** — `IFrameScheduler`, `IGameClock`
- **[08 — Intervals](08-intervals.md)** — cutscene/tween timelines riding Panda's C++ interval system; awaitable, scrubbable
- **[09 — Actors & Animation](09-actors-animation.md)** — `IActor` over `Character`/`AnimControl`/`PartBundle`; subparts, joints, `ActorInterval`, cross-fades
- **[10 — GUI](10-gui.md)** — explicit `Widget` classes over PGui; observables, recipes for composites
- **[11 — Physics, Collision & Particles](11-physics-collision.md)** — collision observables over native entries; built-in physics + particles; Bullet recipe
- **[12 — Audio & Misc](12-audio-misc.md)** — audio on native managers/sounds; `IAudio3D`; FSM deferred; blackboard cut
