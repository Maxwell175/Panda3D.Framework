# DX & Maintainability Review — Working Tracker

**Temporary.** Backlog + decisions from the 6-subsystem DX/style review. Delete once drained.

Legend: `[ ]` todo · `[~]` in progress · `[x]` done · **DECIDED** = approach locked, not yet coded · **THINKING** = needs design discussion first.

Guiding principle (from the framework's design): **minimal constraints, maximal flexibility.** A fix that removes the ability to do something manually is usually the wrong fix — prefer making silent failure *loud* over forcing coupling, and prefer *unlocking* a capability over *wrapping* one.

---

## 1 — Ship the XML docs — DECIDED (yes)
- [ ] `Directory.Build.props`: `GenerateDocumentationFile=true` (silence CS1591 if we don't want to doc every member).
- [ ] Backfill the doc gaps this exposes: Actors public surface (zero docs), Input settable binding props (`Deadzone/Invert/Scale`), `NodeLerps` factory family, `Widgets` public props.
- [ ] (ties to #9) strip authoring shorthand `(07)`/`§4`/`docs/..` out of public `<summary>` → move to `<remarks>`.

## 2 — Managed facades vs pass-through wrappers — DECIDED (per case)
Test applied: **unlock a capability** / **collapse a dance + add semantic value** / **pure forward**. First two = do; last = don't.
- [ ] **Clear color** (`IView.ClearColor`) — DECIDED yes. Property **delegates to native for BOTH get and set** (native region/output is the single source of truth — no cached copy); the value it adds is "correct surface + `SetClearColorActive`", not state storage.
- [ ] **Second `ICameraRig`** (`IView.AddCamera`) — DECIDED yes. Unlocks split-screen that the `internal` ctor currently forbids.
- **Particle `Load(.ptf)`** — **v2 NON-GOAL.** Explicitly out of scope now; revisit in a possible v2.
- [ ] **`IIntervalManager.Play/Loop` → `IPlayingInterval`** — DECIDED yes. Thin handle that keeps `CInterval` hidden; expose a **`WhenDone` observable** and make the handle **awaitable** (`GetAwaiter`, consolidating today's `Play`/`PlayAsync`/`WhenDone` into one return type). Keep it thin.
- [ ] **`IAudio` `Sound` handle (no `PlaySfx`)** — DECIDED. Reusable managed `Sound` (`Play/Stop/Volume/Loop`) loaded once; document the 3-D recipe once. No load-and-play convenience (reload-per-call footgun).
- **Collision pusher/queue gap** — DECIDED, see §11.

## 3 — Silent-no-op footguns vs flexibility — DECIDED
Principle applied: **make dead states loud, never force coupling** (manual `RenderFrame()`/clock-tick stays first-class).
- [ ] `WindowOptions.Vsync` ignored — DECIDED: honor it in `CreateOutput` or remove the property.
- [ ] `IViewManager.Main = null!` → DECIDED: throw `InvalidOperationException` with guidance (no flexibility cost).
- [ ] **`FrameCompositionValidator : IHostedService`** (runs last) — DECIDED. Logs one clear warning for known-dead compositions (window open but no render task **and** no `IClockTickSource`; scheduler present but clock never ticked). Constrains nothing — warning self-clears once any render/tick source exists (framework *or* manual). Becomes the home for future "forgot `AddEvents`?" checks.
- [ ] `IAxisBindingSource` public but behavior `internal` → DECIDED: make one binding-source contract public with `Evaluate` (public read-facade over `IInputSampler`), so custom bindings work. Increases flexibility — on principle.

## Sequencing (proposed)
- **Batch 1 — DONE ✅ (74/74 tests green, uncommitted):** doc flag on (41 `.xml` now generate, CS1591 suppressed); `AddFrameTask` + `FrameTaskHost` added, all 7 bespoke hosted services deleted; `Vsync` removed; `IViewManager.Main` throws + `MainOrNull` added (Input uses it); `FrameCompositionValidator` warns on black-window compositions; `IntervalManager.Dispose` idempotent; `IAudio : IDisposable`; blanket `catch {}` → `IsEmpty()` guards (Widget/Label/ViewInput); tap threshold → `ButtonAction.TapThresholdSeconds`.
  - *Deferred to a later polish pass (low value, non-blocking):* exhaustive `ArgumentNullException.ThrowIfNull` sweep, `FrameTaskDiagnostics` static→`ILogger`, full magic-number sweep (`View.cs` bit/sort consts), doc-gap backfill (Actors etc.) + stripping `(07)`-style shorthand from public summaries.
- **Batch 2 — structural — MOSTLY DONE ✅ (74/74 green, uncommitted):**
  - #12 **`Particles` extracted** into its own project (+ `Panda3D.Framework.Particles.Tests`), removed from Physics, added to the sln.
  - #8 **renames:** `AddContext`→`CreateContext` (provider extension; `InputRuntime.AddContext` instance method kept), `HostedTaskExtensions`→`HostingServiceCollectionExtensions`, collision `EventHandler`→`Handler`, `SliderBarWidget`→`SliderBarBase`.
  - #7 **options relocated:** `WindowOptions`→Rendering, `ClockOptions`→Scheduling; `Abstractions/Options.cs` retired.
  - #6 **one-type-per-file — DONE.** All four `Contracts.cs` retired (Rendering→7, Actors→9, Audio→2, Gui→2); `Gui/Widgets.cs`→8 widget files; interval family split (`Composition.cs`→7, `ManagedInterval.cs`→`Call`/`Lerp<T>`/`Interpolators` + base, `IInterval.cs`→`Ease`/`INativeIntervalSource`); `Input/Bindings.cs`→8 public binding files + `InputSampling.cs` (internal seams); `Input/Actions.cs`→4; `DataGraph.cs`→+`InputRegistry.cs`; `PandaFrameTask.cs`→+2; `IFrameScheduler.cs`→+`IScheduledTask.cs`; `Widget.cs`→+`GuiEventNames.cs`; `ActorTimelines.cs`→+`ActorInterval.cs`; `HostingServiceCollectionExtensions.cs`→+`HostedTaskDescriptor.cs`/`HostedTaskRunner.cs`.
  - **Deliberate exceptions (judgment calls):** `Widget` + `Widget<TItem>` kept together (same-name base+generic — the standard C# co-location); the four one-line internal input-evaluation seams (`IInputSampler`/`IAxisEvaluable`/`IVectorEvaluable`/`Processors`) grouped in `InputSampling.cs` rather than scattered.
- **Batch 3 — API additions (decided facades) — IN PROGRESS:**
  - ✅ **3a typed accessors (part of #4):** `NamedEvent.Get<T>`/`TryGet<T>`/`Count`; `IActor.TryAnim`; `AnimClip` record + `IActorLoader.Load(model, params AnimClip[])`. 76/76 (+2 tests). Purely additive.
  - ✅ **3b `ButtonId` (part of #4):** `ButtonId` (readonly struct, no implicit int; `Name`/`FromName` = stable registry name for save/load; `[JsonConverter]` serializes by name) + `Keys`/`Mouse`/`Gamepad` vocabulary. Rationale confirmed against Panda internals: button ints are **unstable runtime registry indices**, no enum exists, so name-based `ButtonId` is the correct serializable identity — not a hollow wrapper. Routed through the whole input stack (bindings, `IInputSampler`, `DeviceSampler`, `BoundButtons`/contention, `IInput`, `ButtonLabels`); migrated RoamingRalph (`KeyboardButton.Up()`→`Keys.Up`, deleted its `Key(char)` helper) + input tests. 78/78 (+2 tests incl. JSON round-trip).
  - ✅ **3c `OverlayAnchor` enum (part of #4):** string-keyed `IView.OverlayAnchors` → `IReadOnlyDictionary<OverlayAnchor, NodePath>` (9-value enum: Center/Top/Bottom/Left/Right + 4 corners). Retyped through `View` (create + position loops), sample, and the rendering integration test. 15/15 rendering green.
  - ✅ **3d `IView.ClearColor` + `AddCamera` (part of #2):** `ClearColor` (`LVecBase4f`) reads/writes the native output directly (single source of truth) and a set also flips `SetClearColorActive(true)` — collapses the two-call dance. `AddCamera(NodePath? scene = null)` builds a second `CameraRig` (main camera now goes through it too) + `Cameras` list; unlocks split-screen (bind via `DisplayRegionOptions.Camera`). Migrated sample (`View.Output.SetClearColor…`→`View.ClearColor =`) and `UseBlackBackground` helper; +2 probes (ClearColor round-trip, AddCamera split-screen). 15/15 rendering green.
  - ✅ **3e `IPlayingInterval` + `ISound` (part of #2):** `IIntervalManager.Play`/`Loop` → `IPlayingInterval` (thin handle: `IsPlaying`/`Time`/`PlayRate`/`Pause`/`Resume`/`Finish`/`Native` + awaitable-observable `Completed`); dropped `PlayAsync`/`WhenDone(CInterval)` (consolidated). Audio: `LoadSfx`/`LoadMusic` → reusable `ISound` (`Play`/`Stop`/`Volume`/`Loop`/`IsPlaying`/`Native` + `Finished`) loaded once; `Wrap(AudioSound)` adopts sounds obtained elsewhere; dropped `WhenFinished`. **DX finding:** making the *handle* awaitable via its own `GetAwaiter` made every fire-and-forget `Play(iv)` trip CS4014 ("not awaited") — the common case. Fixed by making the *observable* (`Completed`/`Finished`) awaitable instead (Rx's `await IObservable` via `System.Reactive.Linq`), so fire-and-forget stays warning-free. Migrated 3 interval consumers (`SetT`→`Time`, `WhenDone`→`Completed`) + the audio test; +1 interval control test, +control assertions on the audio test. 79/79.
  - ✅ **3f Collision facades (#11):** `RaycastHit` record struct + `ICollisionWorld.Raycast(origin, dir, against, mask?)` and `Pick(camera, filmX, filmY, against, mask?)` (one-shot self-traversing queries; all geometry reported in against-space, distance via `GetRelativePoint`). Typed handler helpers `AddPusher`/`AddFloor`/`AddGravity`/`AddQuery` (physical handlers derive from `CollisionHandlerEvent`, so observables still fire; a queue is polled). Traverse opt-out: `AddCollision(bool autoTraverse = true)` — pass false to own the "clear → traverse → read" timing. Debug `ShowColliders(root)`/`HideColliders()`. +3 tests (Raycast into/distance verified at 8.0, AddPusher still feeds observables, AddQuery polled queue). 82/82.
  - **Batch 3 DONE ✅.**
  - ✅ **RoamingRalph migrated onto the facades (game-jam DX demonstration).** Dropped the parallel native `CollisionTraverser` + the hand-rolled clear/traverse/sort/read machinery. Now `AddCollision(autoTraverse: false)` and the game owns **one** "move → traverse → read" pass: `_collisions.AddPusher(body, Ralph.Node).Horizontal = true` for wall-slide, two `AddQuery` ground rays, a single `_collisions.Traverse(scene.Root)` in Move, then `groundQuery.NearestInto("terrain")` reads terrain height. Validated against the *real* `world.egg` polygon terrain with throwaway headless tests (`into=terrain`, `z=-0.726`; both the raycast and the query path; deleted after).
    - **Correction after review feedback:** first cut used a per-frame `world.Raycast` for terrain-follow — clean, but it runs its *own* traverse, so with the pusher's auto-traverse that was **3 traverses/frame**. `Raycast` is a synchronous *one-shot* (called at slot 0, before the collision slot, needs an answer *now*), so it can't ride the frame traverse. For a *continuous* per-frame query the right tool is a persistent query on the shared traverse → `AddQuery`. Both pusher and queries register on the world's one `_traverser`, so the **default** per-frame traverse (slot 30) drives them all in **1 pass** — no `autoTraverse:false` needed (a brief detour there was reverted). Trade-off owned: reading the query in Move (slot 0) trails the traverse by one frame — negligible for a walking character; `autoTraverse:false` + a self-driven `Traverse` is the escape hatch when you need move→traverse→read in lockstep (fast/precise controllers).
    - **New: `AddQuery` returns `ICollisionQuery`** (was raw `CollisionHandlerQueue`) — `Hits`/`Nearest`/`NearestInto(name)`/`Native`, collapsing the sort/loop/name-filter so the polled path is as clean as `Raycast` while staying on the shared traverse. Establishes the tool split: `Raycast` = value-added one-shot (`RaycastHit` with distance); `AddQuery` = continuous polled (engine `CollisionEntry`s, like the reactive streams). +`NearestInto` assertion on the AddQuery test.
- **Batch 4 — surface hygiene — DONE ✅ (82/82):**
  - #10 **visibility:** `HostedTaskDescriptor` → `internal` (used only within Hosting; fully hidden). `PandaFrameTask`/`FrameTaskDiagnostics`/`IClockTickSource` → `[EditorBrowsable(Advanced)]` (must stay public — cross-assembly + escape hatches — so de-emphasized, not hidden). *Finding:* two live task-registration entry points exist — Scheduling's `AddFrameTask` (module setup, `Func<IServiceProvider,Func<bool>>`) and Hosting's `AddHostedTask` (per-frame `Func<FrameContext,TaskResult>`, used by the gameplay `IFrameScheduler` + tests). Unified in Batch 5 (below).
  - #10 **`IActor.Rig`:** carved joints/subparts/LOD/raw `Part` off `IActor` onto new `IActorRig` (via `actor.Rig`); `Actor` implements both (`Rig => this`), so everyday surface is `Node`/`Character`/`Anim`/`TryAnim`/`Anims` + blending. Migrated tests (`actor.Part()`→`actor.Rig.Part()`, `actor.LodNode`→`actor.Rig.LodNode`).
  - #10 **collision matrix → extensions:** replaced the 12-method `{Entered,Again,Exited}×{By,Into}×{node,name}` matrix on `ICollisionWorld` with 4 composable `IObservable<CollisionEntry>` extensions in `CollisionEntryStreams` (`world.Entered.Into("wall")`). Kept the 3 base streams on the interface. Migrated the one caller.
- **Batch 5 — task-registration unification + polish — DONE ✅ (82/82):**
  - **Unified the two DI-time task registrations.** There were three paths, all materializing `PandaFrameTask`: Scheduling's `AddFrameTask` (factory `Func<IServiceProvider,Func<bool>>`), Hosting's `AddHostedTask` (`Func<FrameContext,TaskResult>`), and runtime `IFrameScheduler.AddFrameTask`. Kept the efficient factory form as fundamental and added a `Func<FrameContext,TaskResult>` **overload** of `AddFrameTask` that adapts onto it (resolves the native clock + lifetime once at start; per-frame builds the `FrameContext` struct — no per-frame service resolution). **Deleted** `AddHostedTask` + `HostedTaskDescriptor` + `HostedTaskRunner`. Now one `AddFrameTask` name at both DI-time (two overloads) and runtime, and the DI-time context form shares `IFrameScheduler`'s exact signature. Migrated the 3 test callers; overload resolution is unambiguous (factory returns `Func<bool>`, context returns `TaskResult`).
  - **Polish:** stripped design-doc chapter shorthand (`(02)`/`§5`/`docs/..`) from 11 public `<summary>` blocks; `ArgumentNullException.ThrowIfNull` sweep (~50 guards across 20 files via backreference sed, message identical via `[CallerArgumentExpression]`); named the 2-D overlay region sort (`View.OverlayRegionSort = 10`, the "2-D over 3-D" invariant); documented public `ActorInterval` (class + ctor). **Skipped** `FrameTaskDiagnostics`→`ILogger` deliberately — `PandaFrameTask.Register` is a static, DI-free primitive called from native trampolines, so injecting a logger would force the coupling the framework avoids; the static event stays as a flexible hook (already `[EditorBrowsable(Advanced)]`).
- After each batch: run the 74-test suite; add tests for new API (ClearColor, Raycast, Sound, IPlayingInterval, validator).
- **Follow-on (separate effort, own design doc):** `Panda3D.Framework.Physics.Bullet` (§12).

## 4 — Weakly-typed / stringly-typed APIs — DECIDED (mostly mechanical)
- [ ] Event payloads `object[]` / `IReadOnlyList<object>` → typed accessors `NamedEvent.Get<T>` / `Observe<T>`.
- [ ] Buttons raw `int` → `readonly struct ButtonId` + `KeyboardButton.AsciiKey(char)` / WASD preset (sample wrote its own `Key()`).
- [ ] `OverlayAnchors["top-left"]` string dict → `OverlayAnchor` enum.
- [ ] Actor anims `Dictionary<string,string>` → `AnimClip(Name,File)` record/overload; add `TryAnim`.
- [ ] Actor `PartJointKey` `$"{part}\0{joint}"` → `readonly record struct` key.

## 5 — Hosted-service boilerplate → `AddFrameTask` helper — DECIDED (yes)
- [ ] Add `services.AddFrameTask(name, sort, tick)` (or a `FrameTaskHostedService` base).
- [ ] Migrate the ~7 copies (Audio/Events/Input/Intervals/Collision/Particles/Render), deleting the bespoke `*HostedService`/`*PumpService`/`*StepService`/`*LoopService` classes and their naming drift.

## 6 — One type per file — DECIDED (yes, always)
- [ ] Split `Contracts.cs` files (Rendering/Actors/Audio/Gui) into one interface/type per file.
- [ ] Split `Widgets.cs` (489 lines/8 types), `DataGraph.cs` (2 types), `PandaFrameTask.cs` (3 types).
- [ ] Move non-contract types out of interface files: `Ease`/`EaseExtensions` out of `IInterval.cs`, `IParticles` out of `ICollisionWorld.cs`, config types out of `Actors/Contracts.cs`.
- [ ] Pick the naming convention (proposal: `IFoo.cs` per interface + `Foo.cs` per impl; retire `Contracts.cs`).

## 7 — Options / config shape — DECIDED (hybrid)
Goal: **zero-plumbing game-jam path** — sensible defaults, no settings screen required; `IConfiguration` binding available but never in the way.
- [ ] Options POCOs config-bindable: parameterless ctor, flat scalar `{ get; set; }`, **no `ValueTuple`s** (`WindowOptions { int Width; int Height; string Title; bool Vsync; }`).
- [ ] Uniform mechanism: `AddX(Action<TOptions>? configure = null)` → `services.Configure<TOptions>`; impls inject `IOptions<TOptions>`. Config binding comes free (`o => cfg.GetSection("Window").Bind(o)`); add explicit `IConfiguration` overloads only if we want them prettier.
- [ ] Runtime value shapes = named record structs (`WindowSize(int Width,int Height)`, `Rect(...)`) on the *live* API, separate from bindable options (also settles §9 value-shape inconsistency).
- [ ] Options co-located per subsystem, one per file; retire `Abstractions/Options.cs`. Abstractions keeps only cross-cutting contracts.
- [ ] Demand-driven — only where a real global knob exists (no empty options classes). Per-call config like `ActorOptions` on `Load(...)` stays a method arg (correct as-is).

## 11 — Built-in collision scope — DECIDED
The built-in collision system is what most simple games use for characters (fall/floor pushers + basic linear jump). Resolutions:
1. **`Raycast`/`Pick` first-class** — `RaycastHit { NodePath Into; LPoint3 SurfacePoint; LVector3 SurfaceNormal; float Distance; }` (exposes the into-node so you can branch on its name, matching the model-collide idiom).
2. **Typed handler-attach helpers** — `AddPusher`/`AddQuery`/`AddFloor`/`AddGravity` over today's raw-native `Add(collider, handler)`, so the fall-pusher / linear-jump path is discoverable. Still **no opinionated character controller in core** — the game applies its own jump velocity; a real controller lives in Bullet (§12).
3. **Auto-traverse opt-out-able** — a game can order "clear → traverse → read" in its own task.
4. **Model-embedded colliders are the primary path** — into-colliders authored in models (egg `<Collide>`) are auto-traversed from the scene root and matched by name (existing `*Into(name)` streams); `Add()` is only for from-colliders. Add a debug **`ShowColliders`/`HideColliders`** helper; document model-into as the default story.

## 12 — Physics project layout & backends — DECIDED
- **`Panda3D.Framework.Physics`** = built-in collision only (§11).
- **`Panda3D.Framework.Particles`** = **extracted from Physics** into its own project (particles ≠ physics; not every physics user wants them). Move `ParticleEffect`/`ParticleInterval`/`ParticlesService`/`IParticles`, retarget namespace, own `AddParticles()`. (Also fixes "`IParticles` hidden in `ICollisionWorld.cs`".) → part of the structural batch.
- **`Panda3D.Framework.Physics.Bullet`** = Bullet backend in its own project — `AddBullet()`/`IBulletWorld`, per-frame step, rigid bodies/shapes/constraints, and *this* is where a real character controller (`BulletCharacterControllerNode`) lives. **FOLLOW-ON**: shape now, build as a dedicated effort with its own design doc (net-new, larger than the cleanup). Independent of §11, so nothing here blocks it.
- **ODE** = out of scope; native escape hatch only (raw, superseded by Bullet).
Corrected diagnosis: `ICollisionWorld.Add(collider, handler)` **already accepts** a native queue/pusher/floor/gravity handler (they derive from `CollisionHandlerEvent`, so observables still fire). The capability is present. The *real* gaps that pushed RoamingRalph to a parallel native `CollisionTraverser`:
1. **No raycast/query convenience** — you hand-roll ray collider + `CollisionHandlerQueue` + `SortEntries` + `GetSurfacePoint`. High-value, boilerplate-collapsing, genuine (not a wrapper).
2. **Discoverability** — `Add` takes a *raw native* handler; no typed `AddPusher`/`AddQuery`, so the capability is easy to miss.
3. **Traverse timing** — the world auto-traverses at `FrameSlots.Collision`; a game needing "clear → traverse → read queue" in its own task (ground-follow) can't easily control ordering, so it drives its own traverser.

Rec (pending): add `Raycast(origin, dir, mask) → RaycastHit?` (+ maybe `Pick`); thin typed `AddPusher(collider, node)` / `AddQuery(collider) → query handle` + docs; give games traverse-timing control (opt out of the auto-slot / expose the traverse). **No** character-controller in core (too opinionated — compose from raycast + pusher). Floor/gravity → native for now.

## 8 — Naming / verb overloading — DECIDED (agree)
- [ ] `AddContext` on `IServiceProvider` → `CreateContext` (it creates, doesn't register).
- [ ] `HostedTaskExtensions` → `HostingServiceCollectionExtensions`.
- [ ] Collision `EventHandler` (shadows `System.EventHandler`) → `Handler`; group native escape hatches under `.Native`.
- [ ] `SliderBarWidget` → `SliderBarBase`; reconcile `Label` (not a `Widget`).

## 9 — Small-idiom consistency — DECIDED (agree)
- [ ] Standardize disposal on the `Interlocked.Exchange(ref _disposed,1)` guard; make `IntervalManager.Dispose` idempotent; kill empty `catch {}` in `Widget`/`Label`/`ViewInput`.
- [ ] `IAudio : IDisposable` (siblings already are).
- [ ] `ArgumentNullException.ThrowIfNull` / `ThrowIfNullOrWhiteSpace` sweep.
- [ ] Consistent value shapes (retire abbreviated tuples in favor of named record structs).
- [ ] `FrameTaskDiagnostics` static-mutable-global → injected sink / `ILogger`.
- [ ] Magic literals → named consts (`0.25f` tap threshold, `View.cs` bit/sort numbers, task-name strings).

## 10 — Public-surface hygiene (go a level deeper) — DECIDED (agree)
- [ ] `PandaFrameTask`/`FrameTaskDiagnostics`/`HostedTaskDescriptor`/`IClockTickSource` → `internal` or `[EditorBrowsable(Advanced)]`.
- [ ] `IActor`: carve advanced rigging (joints/subparts/LOD) onto `actor.Rig.*` or extensions; keep the 5% (`Node`/`Anim`/`Anims`) front-and-center.
- [ ] `ICollisionWorld` 12-method Cartesian matrix → extension methods / fluent `On(phase).By(...)`.

## 13 — Multiple controllers / couch co-op — THINKING (design gap)
The rendering stack already supports split-screen (multi-view: `IView.AddCamera`, per-view regions — see §2), but **input has no per-player concept.** Everything funnels through one sampler:
- `DeviceSampler.IsDown(ButtonId)` **aggregates ALL devices** — it loops `_devices.GetDevices()` and returns true if *any* device reports the button down. `Axis(...)` returns the first device that reports a value.
- `InputRuntime` holds a **single `_sampler`** (`SetSampler`); context contention (`ClaimedSampler`) is therefore **process-wide**, not per-player.
- `IDevices` (`All`/`Connected`/`Disconnected` + hotplug observables) exposes the hardware list but nothing binds a device to a *player*. A `// Multi-window per-player input is bring-your-own` note is the only acknowledgment.

Net effect: two gamepads both drive player 1. Couch co-op is currently BYO — the game would have to bypass the framework input model and sample devices directly.

**`ButtonId` stays correct here** — the *vocabulary* (which buttons/axes exist) is shared across players; only the *source* (which physical device answers) differs per player. So this is a sampler/scope problem, not an identity problem. Good that 3b landed name-based `ButtonId` first.

Proposed direction (not yet decided — keep minimal-constraints/maximal-flexibility):
1. **Device-scoped sampler** — a sampler bound to one (or a set of) `InputDevice` instead of aggregating all. The existing all-devices sampler stays the default (single-player path unchanged).
2. **Per-player input scope** — a `Player` (or `IInputScope`) that owns: a device assignment + its own contexts/actions + its own contention. `InputRuntime` gains the ability to host N scopes rather than one global sampler; today's global API becomes "player 0" so nothing breaks.
3. **Join/assignment flow** — a "press A to join" helper over `IDevices` hotplug observables that hands a newly-pressed device to the next player scope. Keep it optional; assignment can also be manual/explicit.

Open questions: keyboard-sharing (split halves of one keyboard across two players — a real couch-coop case), whether player scopes couple to `IView`s (they shouldn't have to — input scope ≠ render view), and how contention composes when a device is shared.

Not scheduled into a batch yet — larger than the current cleanup and net-new API. Slot after Batch 3 API additions, or spin its own design doc alongside the Bullet follow-on (§12).
