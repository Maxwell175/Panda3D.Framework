# 08 — Intervals (`Panda3D.Framework.Intervals`)

**Purpose.** Cutscene and tweening timelines: lerp intervals, sequences, parallels, waits, and callbacks — declarative, pausable, reversible, scrubbable, and awaitable. Built **on Panda's own C++ interval system** (`CInterval` and friends), not a managed re-implementation: common cutscene lerps run at C++ speed with zero per-frame managed↔native chatter, while managed intervals (game logic, custom easing) participate inside the same timeline through the engine's external-interval mechanism.

**Replaces in `direct`.** `direct.interval` — the `Lerp*Interval` family, `Sequence`, `Parallel`, `Wait`, `Func`, and the global `ivalMgr`/`ivalLoop`. (Python's lerp/meta intervals are thin sugar over the same C++ classes we use; the 13-class `LerpPosHprScaleShear…` combinatorial family collapses into one C# method with optional parameters.)

**Dependencies.** `Abstractions`; `Scheduling` (the step task); `Events` (awaitable completion rides the done-event through the pump); the fork's C# bindings — note this is the first project that needs the **`panda3d.direct`** binding module (the C++ interval classes live in `direct/src/interval`/libp3direct, which the fork interrogates for C# alongside the core).

## Ride the engine's interval machine

`CInterval` (base) already publishes the whole cutscene control surface: `Start(startT, endT, playRate)` (sub-range playback at any rate), `Loop()`, `Pause()`/`Resume()`/`ResumeUntil(t)`, `Finish()` (jump to end state — the skip button), `SetT(t)` (**scrubbing**), `SetPlayRate`, `IsPlaying`, `SetAutoPause`/`SetAutoFinish`, `SetDoneEvent(name)`, `ClearToInitial()`. `CMetaInterval` is the timeline (items placed relative to the previous item's end = sequence, or begin = parallel; levels nest). `CLerpNodePathInterval` lerps transform (pos/hpr/quat/scale/shear, in any combination in **one** interval), color, color-scale, and texture transform; it can capture start values at play time (`bake_in_start` — no `from` required), lerp **relative to another node** (`other` — a dolly tracked to a moving target), and move **fluidly** (`fluid` — uses `set_fluid_pos` so the collision pass sees continuous motion, not teleports; the prev-transform reset that makes this meaningful runs at `FrameSlots.PrevTransform`, registered by [11](11-physics-collision.md)'s `AddCollision`). `WaitInterval`, `ShowInterval`, `HideInterval` cover the rest. A `CIntervalManager` is *constructible* (not forced-global), stepped once per frame; `Interrupt()` pauses/finishes everything tagged auto-pause/auto-finish (scene transitions).

Per the wrap rule ([01](01-abstractions.md)): **playback control is the binding surface, used directly** — `Play` hands back the native `CInterval` and you call `Pause`/`SetT`/`Finish`/`SetPlayRate` on it. The framework adds only what's missing: composition ergonomics, managed participation, awaitability, and DI-scoped management.

**Public surface.**
```csharp
// ---- Composition currency ----
public interface IInterval { double Duration { get; } }      // what Sequence/Parallel/Play accept

// ---- Composition: explicit classes; flattened into a native CMetaInterval at Play ----
public sealed class Sequence : IInterval {                    // items run one after another
    public Sequence(params IInterval[] items);
    public Sequence(string name, params IInterval[] items);
    public IList<IInterval> Items { get; }                    // mutable; edits after a Play take effect on the next Play (reflatten)
}
public sealed class Parallel : IInterval {                    // items start together
    public Parallel(params IInterval[] items);
    public Parallel(string name, params IInterval[] items);
    public IList<IInterval> Items { get; }
}
public sealed class Wait : IInterval { public Wait(double seconds); }                  // -> WaitInterval
public sealed class Show : IInterval { public Show(NodePath node); }                  // -> ShowInterval
public sealed class Hide : IInterval { public Hide(NodePath node); }                  // -> HideInterval
public sealed class FromNative : IInterval { public FromNative(CInterval interval); } // adopt any raw binding interval

// ---- Node lerps: factories over CLerpNodePathInterval (C++ speed; engine vocabulary) ----
public enum Ease { None, In, Out, InOut }                     // maps 1:1 to CLerpInterval BlendType (the native set)
public static class NodeLerps {                               // extension methods on NodePath
    // Single-property lerps. Omitted `from` = bake_in_start (captured when the lerp starts playing).
    public static IInterval PosTo   (this NodePath n, LVecBase3f pos,    double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null, bool fluid = false);
    public static IInterval HprTo   (this NodePath n, LVecBase3f hpr,    double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null);
    public static IInterval QuatTo  (this NodePath n, LQuaternionf quat, double dur, Ease ease = Ease.None, LQuaternionf? from = null, NodePath? other = null);
    public static IInterval ScaleTo (this NodePath n, LVecBase3f scale,  double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null);
    public static IInterval ScaleTo (this NodePath n, float scale,      double dur, Ease ease = Ease.None);
    public static IInterval ShearTo (this NodePath n, LVecBase3f shear,  double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null);
    public static IInterval ColorTo (this NodePath n, LVecBase4f color,     double dur, Ease ease = Ease.None, LVecBase4f? from = null);
    public static IInterval ColorScaleTo(this NodePath n, LVecBase4f scale, double dur, Ease ease = Ease.None, LVecBase4f? from = null);
    public static IInterval AlphaTo (this NodePath n, float alpha,      double dur, Ease ease = Ease.None);   // convenience: color-scale alpha only

    // Combined transform lerp — ONE native interval lerping any subset simultaneously.
    // Replaces Python's LerpPosHprInterval / LerpPosQuatScaleShearInterval / … combinatorial family.
    public static IInterval TransformTo(this NodePath n, double dur,
        LVecBase3f? pos = null, LVecBase3f? hpr = null, LQuaternionf? quat = null,
        LVecBase3f? scale = null, LVecBase3f? shear = null,
        Ease ease = Ease.None, NodePath? other = null, bool fluid = false);   // bake_in_start for all supplied properties

    // Texture-transform lerps (optional stage; null = the default TextureStage).
    public static IInterval TexOffsetTo(this NodePath n, LVecBase2f offset, double dur, Ease ease = Ease.None, TextureStage? stage = null);
    public static IInterval TexRotateTo(this NodePath n, float degrees,    double dur, Ease ease = Ease.None, TextureStage? stage = null);
    public static IInterval TexScaleTo (this NodePath n, LVecBase2f scale,  double dur, Ease ease = Ease.None, TextureStage? stage = null);
}

// ---- Managed intervals: game logic inside the timeline (ext-index mechanism) ----
public abstract class ManagedInterval : IInterval {           // derive for arbitrary stepped logic
    protected ManagedInterval(double duration, bool openEnded = false);
    public double Duration { get; }
    public virtual void Initialize(double t) { }              // ET_initialize; reverse variants also relayed
    public abstract  void Step(double t);                     // t in [0, Duration]; called while playing AND when scrubbed
    public virtual  void Complete() { }                    // ET_finalize; named to avoid object.Finalize
}
public sealed class Call : ManagedInterval { public Call(Action action); }   // zero-duration side effect
public sealed class Lerp<T> : ManagedInterval {               // the managed lerp: custom easing + any target
    public Lerp(T from, T to, double dur, Action<T> set, Func<double,double>? ease = null);
    // T supported out of the box: float, double, LVecBase2f/3f/4f;
    // ease: any curve (bounce, elastic, authored) — the escape hatch beyond the native four blends
}
public sealed class SoundInterval : ManagedInterval {         // HOLDS the timeline while a sound plays
    public SoundInterval(AudioSound sound, bool loop = false, double duration = 0,   // 0 = sound length - startTime
                         float volume = 1f, double startTime = 0, NodePath? emitter = null);  // emitter: 3-D positional (12)
    // Initialize(t): seek to t+startTime and play. Step(t): re-sync (seek) when scrubbed/desynced.
    // Interrupt/Complete: stop. Finish() silences it.
}
// ParticleInterval is defined in Panda3D.Framework.Physics next to ParticleEffect, and composes here.
public sealed class ParticleInterval : ManagedInterval {      // HOLDS the timeline while a particle effect runs
    public ParticleInterval(ParticleEffect effect,             // [11](11-physics-collision.md)'s effect type
                            NodePath parent, double duration, double softStopT = 0, bool cleanup = false);
    // Initialize: SoftStart the effect under `parent`. At duration - softStopT: SoftStop (emission off, live
    // particles drain). Complete: stop (+ optionally cleanup/dispose).
}

// ---- Manager: DI-scoped; owns a constructed CIntervalManager + the step task ----
public interface IIntervalManager : IDisposable {
    CInterval Play(IInterval interval);                        // flatten, attach, Start(); returns the NATIVE handle
    CInterval Loop(IInterval interval);                        // same, but native Loop()
    PandaTask  PlayAsync(IInterval interval);                  // Play + WhenDone convenience
    PandaTask  WhenDone(CInterval playing);                    // done_event -> pump -> completion; already-finished => completes immediately
    void Interrupt();                                          // pause/finish everything tagged auto-pause/auto-finish
}
public static class IntervalsServiceCollectionExtensions {
    public static IServiceCollection AddIntervals(this IServiceCollection s);  // app manager + step task at FrameSlots.Intervals
}
```

**Usage — a cutscene.**
```csharp
var cutscene = new Sequence("intro",
    new Parallel(
        cam.Node.PosTo(new(0, -20, 6), 4, Ease.InOut, other: door),    // dolly relative to the door
        hero.TransformTo(4, pos: doorstep, hpr: faceDoor, ease: Ease.InOut),  // pos+hpr in ONE native lerp
        new Lerp<float>(1f, 0f, 4, a => hud.Alpha = a)),               // game logic tweened in the same timeline
    new Wait(0.5),
    new Call(() => door.Unlock()),
    new Parallel(
        door.HprTo(new(90, 0, 0), 1.5, Ease.Out),
        new SoundInterval(doorCreak, emitter: door)));   // sound holds its slot; pausing the cutscene pauses it

var play = intervals.Play(cutscene);                    // native CInterval handle
skip.Clicked.Subscribe(_ => play.Finish());             // skip = Finish(): jumps to the end state
await intervals.WhenDone(play);                         // coroutine-friendly; resumes in the gameplay slot
```
Skippable, pausable (`play.Pause()`), scrubbable (`play.SetT(2.5)` — a cutscene editor is a slider bound to `SetT`), rate-adjustable (`play.SetPlayRate(0.25)`), and reusable (`intervals.Play(cutscene)` again later replays it), all on the native handle.

**Design notes.**
- **Flattening.** `Sequence`/`Parallel` are managed *descriptions* (mutable `Items`, matching the explicit-mutable style of [05](05-input.md)); each `Play` flattens the current tree into one native `CMetaInterval` — native children via `add_c_interval` at `RS_previous_end`/`RS_previous_begin` (with `push_level`/`pop_level` for nesting), managed children via `add_ext_index`. Replaying the same description reflattens from current `Items`, so mutations naturally take effect on the next `Play`.
- **Managed dispatch.** After each `CIntervalManager.step()`, the manager drains `get_next_event()`/`get_next_removal()` and relays the recorded event type to the corresponding `ManagedInterval` — Python's `__doPythonCallbacks`, verbatim. `CMetaInterval` enqueues **both forward and reverse** ext events (`do_event_forward`/`do_event_reverse`, verified in source) whichever direction `t` moves — so managed children honor pause, scrub, and reverse exactly like native ones.
- **Done-event lifecycle.** `Play` assigns a unique `SetDoneEvent("ival-done-{id}")`; completion queues that event on the manager's queue — which **defaults to the global queue**, so it flows through the [Events](06-events.md) pump with zero config. `PlayAsync` subscribes *before* `Start()` (no race); `WhenDone` on an already-finished handle (`GetState() == S_final`) completes immediately. The subscription disposes itself after firing.
- **One combined lerp beats a `Parallel` of singles.** `TransformTo` drives pos/hpr/scale/shear from a single `CLerpNodePathInterval` — one native step per frame instead of four, and no risk of the properties drifting out of phase. Prefer it whenever multiple transform properties tween together.
- **Duration-holding intervals.** `SoundInterval`/`ParticleInterval` (and [09](09-actors-animation.md)'s `ActorInterval`) aren't fire-and-forget `Call`s — they occupy their full duration in the timeline, so a following `Sequence` item waits for them, and scrub/`Finish()` propagate into the sound/effect/animation via the ext event relay (`Initialize`/`Step`/`Interrupt`/`Complete`). That propagation is the reason they're intervals rather than callbacks.
- **Native easing is four blends; custom curves are managed.** `Ease` maps 1:1 to `CLerpInterval`'s `BT_no_blend/BT_ease_in/BT_ease_out/BT_ease_in_out` — that's all the native lerp supports. Anything fancier (bounce, elastic, authored curves) is a `Lerp<T>` with a custom `ease` function: managed-callback cost per frame, unlimited flexibility.
- **Stepping and scope.** `AddIntervals` registers the app-wide manager and its step task at `FrameSlots.Intervals` (20, the `ivalLoop` slot). The manager is a normal DI service — additional scoped managers are possible (a cutscene scope that `Interrupt()`s on disposal); nothing is global. Intervals mutate the scene, so stepping stays on the `"default"` chain.
- **`Finish()` vs `Pause()` vs `ClearToInitial()` on teardown.** `Finish` runs the timeline to its end state (doors end open); `Pause` freezes mid-flight; `ClearToInitial` rewinds to the pristine start. `Interrupt()` applies each interval's own `auto_pause`/`auto_finish` tag — set the tag at authoring time for per-interval transition behavior.

**Non-features (v1).** Python interval types deliberately not carried: `MopathInterval` (motion-path following — needs the mopath classes; revisit if curves become a need), `ProjectileInterval` (ballistic arc — a few lines of `Lerp<T>`/gameplay math), `IndirectInterval` (sub-range-as-item — the native `Start(startT, endT, playRate)` already covers ad-hoc sub-range playback). `ActorInterval` is **in scope but defined in [09](09-actors-animation.md)** — it holds the timeline for the animation's frame range and sets the pose from `t` each step (which is exactly what makes scrub/reverse work for animations in cutscenes); `CLerpAnimEffectInterval` (native weight blending) composes here too.

**Open items.**
- (none)

> **Verified:** `IntervalDrivingTests` exercise `CMetaInterval.add_ext_index`, `CIntervalManager.get_next_event`/`get_next_removal`, native node lerps, scrubbing, managed `Call`/`Lerp<T>`, and `SoundInterval`. `ParticlesTests.ParticleIntervalParentsSoftStopsAndCleansUpEffect` covers the particle timeline adapter in the Physics assembly.

> **Verified (source, fork + upstream):** The fork's CMake generates C# bindings for the **`panda3d.direct`** module (`add_csharp_module(panda3d.direct …)`), which includes `direct/src/interval`. `CInterval` PUBLISHED: `start(start_t,end_t,play_rate)`, `loop`, `pause` (returns t), `resume`, `resume_until`, `finish`, `clear_to_initial`, `set_t`/`get_t`, `set_play_rate`, `is_playing`, `get_state`, `set_auto_pause`/`set_auto_finish`, `set_done_event`, `set_manager`. `CMetaInterval` PUBLISHED: `add_c_interval(ival, rel_time, RS_previous_end|RS_previous_begin|RS_level_begin)`, `push_level`/`pop_level`, `add_ext_index`, `set_interval_start_time`; its `.cxx` enqueues ext-child events through **both** `do_event_forward` and `do_event_reverse`, so scrubbing/reversing relays to external intervals. `CLerpNodePathInterval` PUBLISHED ctor `(name, duration, blend_type, bake_in_start, fluid, node, other)` + start/end setters for pos/hpr/quat/scale/shear/color/color_scale and tex offset/rotate/scale (`set_texture_stage`); `fluid` maps to fluid-position updates (collision-continuous motion). `CLerpInterval.BlendType`: `BT_no_blend`, `BT_ease_in`, `BT_ease_out`, `BT_ease_in_out`. `WaitInterval(duration)`, `ShowInterval(node)`, `HideInterval(node)` PUBLISHED. `CIntervalManager` PUBLISHED: constructible, `step()`, `interrupt()`, `add_c_interval(ival, external)`, `get_next_event()`/`get_next_removal()`, `set_event_queue` — constructor defaults the event queue to `EventQueue::get_global_event_queue()`, so done-events reach the framework's pump unmodified. Python's `IntervalManager.step()` = C++ `step()` + the `getNextRemoval`/`getNextEvent` drain — the exact pattern the managed dispatch reuses.

**See also.** [07 Scheduling & Time](07-scheduling-and-time.md) (the step task, `FrameSlots.Intervals`); [06 Events](06-events.md) (done-event → pump → `WhenDone`); [09 Actors & Animation](09-actors-animation.md) (`CLerpAnimEffectInterval` blends animation weights inside these timelines); [12 Audio & Misc](12-audio-misc.md) (sound cues via `Call`/`ManagedInterval`).
