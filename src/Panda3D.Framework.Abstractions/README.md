# Panda3D.Framework.Abstractions

The contracts package every other Panda3D.Framework library and all gameplay code compile against: interfaces, options types, shared parameter types, and the frame-slot constants. No behavior, no registration — this is the seam that keeps the framework container-agnostic and lets client and server share one gameplay assembly.

## Provides

- `IGameClock` — injected, read-only view of the engine's global clock (`Dt`, `FrameTime`, `RealTime`, `FrameCount`).
- `IFrameScheduler` / `IScheduledTask` — the gameplay-facing scheduling contract (`AddFrameTask`, `AddTimed`, `AddFixedStep`, `DelayFrames`); handles are disposable.
- `ISceneManager` — the 3-D world roots: a default `render` root plus named, get-or-create independent roots.
- `FrameContext` / `TaskResult` — the per-frame task signature shared by host tasks and scheduler tasks, so both live in one ordering space.
- `FrameSlots` — the shared per-frame sort scale (`DataLoop` −50, `Events` −1, `Gameplay` 0, `Intervals` 20, `Collision` 30, `Render` 50, `Audio` 60, …).
- `IClockTickSource` — marker for a service that already advances the clock (so `AddClock` won't double-tick).

The per-frame task shape both registration paths use:

```csharp
TaskResult Tick(FrameContext ctx)  // ctx: Services, Dt, Stopping
    => keepGoing ? TaskResult.Continue : TaskResult.Done;
```

Depends only on `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options` (abstractions only) — no concrete container, no hosting. Implementations and the `AddXxx()` registration methods live in the owning modules (Hosting, Scheduling, Rendering, …), never here.

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
