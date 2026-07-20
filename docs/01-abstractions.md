# 01 — Abstractions (`Panda3D.Framework.Abstractions`)

**Purpose.** The single contracts package every other project and all gameplay code depends on. It holds interfaces, options types, event types that are genuinely cross-cutting, and shared constants (frame slots, service keys) — no behavior, no registration. It is the seam that makes the whole library container-agnostic and the seam that lets client and server share gameplay code.

**Replaces in `direct`.** Nothing directly; `direct` has no abstraction layer (its "contract" was the global `base` object). This package is the structural fix for that.

**Dependencies.**
- `Microsoft.Extensions.DependencyInjection.Abstractions` (for `IServiceCollection`/`IServiceProvider`/`ServiceLifetime`).
- `Microsoft.Extensions.Options` (for `IOptions<T>` patterns) — abstractions only.
- The C# bindings (`Maxwell175/panda3d` fork, `csharp` branch) + `Panda3d.Async`. Per the **wrap rule** below, the framework uses the generated binding type directly: concrete classes for simple-inheritance types (`NodePath`, `Lens`, `AudioSound`, …), and generated interfaces only for types that need multiple-inheritance compatibility (`IGraphicsOutput`, `IDisplayRegion`, …). The few framework wrappers expose the wrapped object through a descriptively-named accessor (e.g. `IView.Output`, `ICameraRig.Lens`). (A binding member is reachable from C# if it is `PUBLISHED` **or** `CSHARP_EXTENSION` in the fork — that governs what wrapper implementations can call.)
- **No** concrete DI container, **no** `Microsoft.Extensions.Hosting`, **no** other framework project.

**Public surface (representative).**
```csharp
// Time (matches 07)
public interface IGameClock { double Dt { get; } double FrameTime { get; } double RealTime { get; } long FrameCount { get; } }

// Scene roots — simple default AND multiple independent 3-D roots (DS-two-screens shape).
// Implementation is an internal SceneManager in Hosting (trivial NodePath wrangling); see 00 §map.
public interface ISceneManager {
    NodePath Root { get; }                   // the default 3-D world root (render); simple-default path
    NodePath GetRoot(string name);           // named independent 3-D root; get-or-create (idempotent)
}

// Events: objects expose notifications as System.Reactive IObservable<T> (see 06).
// IObservable<T> is built into .NET, so contracts here take no dependency on System.Reactive;
// only implementations/consumers that want Rx operators (or System.Reactive.Unit, used for
// parameterless signals like view.Closed / button.Pressed) reference it.

// Scheduling (full surface in 07)
public interface IFrameScheduler {
    IScheduledTask AddFrameTask(Func<FrameContext, TaskResult> task, int sort = FrameSlots.Gameplay, string? name = null);
    IScheduledTask AddTimed(TimeSpan delay, Action action);
    IScheduledTask AddFixedStep(double hz, Action<double> step, int sort = FrameSlots.Gameplay);
    PandaTask DelayFrames(int n);
}

// Shared frame-task types — used by BOTH the setup-time AddFrameTask (FrameTaskRegistration, in
// Scheduling; see 02/07) and the gameplay-facing IFrameScheduler (07), so gameplay and framework
// tasks share one signature and one ordering space.
public readonly record struct FrameContext(IServiceProvider Services, double Dt, CancellationToken Stopping);
public enum TaskResult { Continue, Done }   // direct's cont/done

// The frame sort convention (adopted from direct/ShowBase; see 00 §5)
public static class FrameSlots {
    public const int PrevTransform = -51;  // reset_all_prev_transforms — required for fluid motion (08/11); registered by AddCollision
    public const int DataLoop  = -50;  // input/data-graph traverse
    public const int Events    =  -1;  // eventManager — drain queued Panda events before gameplay reads observables
    public const int Gameplay  =   0;  // coroutine resumption + user frame tasks (default)
    public const int Intervals =  20;  // ivalLoop analog
    public const int Collision =  30;  // collisionLoop analog
    public const int Render    =  50;  // igLoop analog
    public const int Audio     =  60;  // audioLoop analog
    // (v2 may add a slot for the ECS schedule, e.g. ~10, when it exists)
}

// Options example — TOptions types live in their owning module, next to the AddXxx that binds them
// (WindowOptions in Rendering, ClockOptions in Scheduling, …), not here; shown only for shape.
public sealed class WindowOptions { public (int W, int H) Size { get; set; } = (1280,720); public string Title { get; set; } = "Game"; }
```

**Design notes.**
- **The wrap rule (decided).** The bindings now generate interfaces only where the C++ type participates in multiple inheritance; simple-inheritance types are exposed as concrete partial classes. Public API uses whichever binding shape the generator emits for that type — `NodePath`/`Lens`/`AudioSound` for simple types, `IGraphicsOutput`/`IDisplayRegion` where an interface exists for multiple-inheritance compatibility. Introduce a framework type **only when it adds genuine behavior** — ergonomics that compose several binding calls (`ICameraRig.SetPerspective`), framework state, or lifecycle/scope orchestration (`IView`, `IViewManager`). Never add a wrapper merely to re-expose a binding type or to "add `IDisposable`" (native binding objects already implement it where ownership requires it). Do **not** subclass binding types: the bindings can't override base C++ virtuals, so a subclass gains nothing overridable and only risks name collisions with the C++ surface — wrap by composition instead, exposing the wrapped object through a descriptively-named accessor (`IView.Output : IGraphicsOutput`, `ICameraRig.Lens : Lens`).
- **Naming discipline.** Wrapper members must not shadow-with-different-meaning the wrapped C++ surface (don't define a `SetPos` that behaves differently from `NodePath.set_pos`); either delegate faithfully or pick a distinct name.
- **Binding types used directly.** Scene-graph, rendering, audio, collision, and value primitives are the bindings' own generated types; the framework does not re-wrap them. Some older sketches may still show an `I*` binding name where the current generator now emits a concrete class; read those as "the binding type directly" unless the type appears in the wrapper inventory below.
- **Math/value types are Panda's too.** Vectors, points, colors, matrices, and quaternions are the engine's `LVecBase`/`LVector*`/`LPoint*`/`LColor`/`LMatrix*`/`LQuaternion` types directly — not a framework `Vector2`/`Vector3`. They're the bindings' own value types, they're what every Panda call already takes and returns, and wrapping them would add a conversion tax at every boundary for no behavior. (Same rule as buttons/axes in [05](05-input.md): prefer the engine type when it suffices.)
- **Registration lives in owning modules.** `AddXxx(this IServiceCollection, Action<TOptions>? configure = null)` extension methods are declared in the module that owns the implementation (they must reference concrete types), and each module's `TOptions` type ships alongside its `AddXxx` (e.g. `WindowOptions` in Rendering, `ClockOptions` in Scheduling) — not here. What lives *here* is what every dependent compiles against: the interfaces, shared event/parameter types (`FrameContext`/`TaskResult`), `FrameSlots`, and service-key constants.
- **Keyed contracts.** Where families exist (named scene roots, multiple cameras, multiple physics backends), the abstraction is consumed with `[FromKeyedServices("…")]`; the keys are documented constants in this package.
- **What does NOT belong here.** Any `new` of engine objects, any `GetGlobalPtr()`, any task scheduling, any `BuildServiceProvider()`, any `AddXxx` body. If a type needs the engine to do its job, its interface goes here and its implementation goes in the owning module.
- **Versioning.** This is the package most likely to be referenced widely; treat breaking changes to it as breaking changes to the whole library and version accordingly.

- **Wrapper inventory (final, post-polish).** Every wrapper was re-tested against the rule during its doc's polish; the survivors, each justified where defined: `IView`/`IViewManager`/`ICameraRig` (03), `ISceneManager` (declared here, implemented in Hosting), the action/binding classes (05), `INamedEventBus` + the pump (06), `IGameClock`/`IFrameScheduler` (07), `Sequence`/`Parallel`/`ManagedInterval` family + `IIntervalManager` (08), `IActor` (09), the `Widget` classes + `IGui` (10), `ICollisionWorld` + `IParticles`/`ParticleEffect` (11), `IAudio`/`IAudio3D` (12). Everything else is the binding type directly.

**Open items.**
- (none)

**See also.** [00 Overview](00-overview.md) §4–5 (cross-cutting rules, frame slots); every other doc references types declared here.
