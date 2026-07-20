# 09 — Actors & Animation (`Panda3D.Framework.Actors`)

**Purpose.** Loading and controlling animated models: name-addressed animation playback, blending, subparts (half-body animation), joint exposure/control, and the timeline pieces (`ActorInterval`, cross-fades) that make animated cutscenes work. Unlike intervals, **there is no C++ Actor** — Python's `Actor` is a ~3,000-line Python class composing the real primitives (`Character`, `AnimControl`, `PartBundle`, `auto_bind`). That composition is genuine added behavior, so `IActor` is one of the few framework wrappers that earns its place under the wrap rule — but playback and blending remain the **native** binding surfaces underneath.

**Replaces in `direct`.** `direct.actor.Actor` — `loadModel`/`loadAnims`, `play`/`loop`/`pose`/`stop`, `enableBlend`/`setControlEffect`, `makeSubpart`, `exposeJoint`/`controlJoint`/`freezeJoint` — and `direct.interval.ActorInterval`.

**Dependencies.** `Abstractions`; `Intervals` (`ActorInterval : ManagedInterval`; cross-fade returns `IInterval`); the fork's C# bindings — core (`Character`, `AnimControl`, `PartBundle`, `Loader`) **and** the `panda3d.direct` module (`CLerpAnimEffectInterval`).

## What's native vs what the actor adds

The engine already publishes the working parts. `AnimInterface` (inherited by `AnimControl`) is the complete playback handle: `Play()`/`Play(from, to)`, `Loop(restart)`/`Loop(restart, from, to)`, `Pingpong`, `Stop`, `Pose(frame)`, `PlayRate`, and full frame queries. `PartBundle` owns blending: `set_anim_blend_flag` (multiple simultaneous anims), `set_control_effect(control, weight)` (per-anim weights), `set_blend_type`, `set_frame_blend_flag` (inter-frame interpolation), plus `bind_anim(anim, flags, subset)` / `load_bind_anim(loader, file, flags, subset, allow_async)` and the joint hooks `control_joint`/`freeze_joint`/`release_joint`. `PartSubset` (include/exclude joints by glob) is the subpart mechanism. `auto_bind(root, collection, flags)` binds everything matching. `Character.set_lod_animation` gives distance-based animation-rate LOD.

What none of that provides — and what Python's `Actor` exists for — is the *composition*: load a model plus a set of anim files, hold the name→`AnimControl` map (per part), configure subparts once and have every later bind respect them, and resolve names for blending/joints. `IActor` is exactly that composition and nothing more: **every playback call returns or resolves to the native handle.**

**Public surface.**
```csharp
public interface IActorLoader {
    PandaTask<IActor> LoadAsync(string model, IReadOnlyDictionary<string,string>? anims = null, ActorOptions? options = null);
    IActor Load(string model, IReadOnlyDictionary<string,string>? anims = null, ActorOptions? options = null);
    IActor Load(string model, params AnimClip[] anims);              // positional name+file clips
    // Multipart (separate geometry per part, e.g. head/torso/legs models):
    PandaTask<IActor> LoadAsync(IReadOnlyDictionary<string, ActorPart> parts, ActorOptions? options = null);
    // The full matrix — parts x geometry LODs (either dimension may be singular):
    PandaTask<IActor> LoadAsync(ActorDefinition definition, ActorOptions? options = null);
}
public readonly record struct AnimClip(string Name, string File);   // a named animation clip (the params overload)
public readonly record struct ActorPart(string Model, IReadOnlyDictionary<string,string>? Anims = null);
public readonly record struct LodLevel(string Name, float SwitchIn, float SwitchOut);   // -> LODNode.add_switch(in, out)
public sealed class ActorDefinition {
    public IList<LodLevel> Lods { get; } = [];                       // empty = no geometry LOD
    public IDictionary<string, ActorPartDef> Parts { get; } = new Dictionary<string, ActorPartDef>();  // "modelRoot" if single
}
public sealed class ActorPartDef {
    public string? Model { get; set; }                               // no-LOD form
    public IDictionary<string, string> ModelByLod { get; } = new Dictionary<string, string>();  // lodName -> model
    public IDictionary<string, string> Anims { get; } = new Dictionary<string, string>();
}
public sealed class ActorOptions {
    public bool FrameBlend { get; set; }          // PartBundle.set_frame_blend_flag: interpolate between frames
    public bool LooseHierarchy { get; set; }      // relax auto_bind/bind_anim hierarchy_match_flags
}

// The default part name is ActorDefaults.DefaultPart == "modelRoot" (shown inline below).
public interface IActor : IDisposable {
    NodePath Node { get; }                         // parent it, move it — a normal node
    Character Character { get; }                   // the binding node itself (escape hatch)

    // ---- Playback: resolves the name, returns the NATIVE handle ----
    IAnimControl Anim(string anim, string part = "modelRoot");   // .Loop(true) / .Play(10, 24) / .Pose(f) / .PlayRate — AnimInterface; throws if unknown
    bool TryAnim(string anim, out IAnimControl? control, string part = "modelRoot");   // non-throwing lookup
    IReadOnlyCollection<string> Anims { get; }

    // ---- Blending: name resolution over PartBundle ----
    void EnableBlend(string part = "modelRoot");                 // set_anim_blend_flag(true)
    void DisableBlend(string part = "modelRoot");
    void SetBlendWeight(string anim, float weight, string part = "modelRoot");    // set_control_effect(control, weight)

    // ---- Advanced rigging (subparts, joints, LOD, raw part bundles) hangs off Rig, NOT IActor directly ----
    IActorRig Rig { get; }
}

// Reached via IActor.Rig — keeps the advanced native surface out of the everyday actor.
public interface IActorRig {
    // ---- Subparts: joint subsets of a part (half-body animation) ----
    void MakeSubpart(string name, SubpartDef def, string parent = "modelRoot");   // PartSubset include/exclude globs
    PartBundle Part(string part = "modelRoot");                  // native bundle: blend_type, frame_blend_flag, …

    // ---- Joints ----
    NodePath ExposeJoint(string joint, string part = "modelRoot", bool local = false);   // joint writes node each frame
    NodePath ControlJoint(string joint, string part = "modelRoot");                      // node drives joint each frame
    void FreezeJoint(string joint, TransformState transform, string part = "modelRoot"); // static override (cheaper)
    void ReleaseJoint(string joint, string part = "modelRoot");

    // ---- Geometry LOD (when loaded with LodLevels) ----
    IReadOnlyList<LodLevel> Lods { get; }          // empty when not a LOD actor
    LODNode? LodNode { get; }                      // native switching control: ForceSwitch(i)/ClearForceSwitch/SetLodScale/SetCenter

    void SetAnimRateLod(LPoint3f center, float far, float near, float delayFactor = 1f); // Character.set_lod_animation
}
public readonly record struct SubpartDef(string[] IncludeJoints, string[] ExcludeJoints);  // glob patterns, e.g. "arm_*"

// ---- Timeline pieces (compose into 08's Sequence/Parallel) ----
public sealed class ActorInterval : ManagedInterval {          // HOLDS the timeline; Step(t) POSES the animation from t
    public ActorInterval(IActor actor, string anim, string part = "modelRoot",
                         bool loop = false, bool constrainedLoop = false,
                         double? duration = null,               // default: the frame range at |playRate|
                         int? startFrame = null, int? endFrame = null, double playRate = 1);
}
public static class ActorTimelines {
    // Cross-fade blend weights over dur: native CLerpAnimEffectInterval(add_control(from, 1→0), add_control(to, 0→1)).
    public static IInterval CrossFade(this IActor actor, string fromAnim, string toAnim, double dur,
                                      Ease ease = Ease.None, string part = "modelRoot");
}

public static class ActorsServiceCollectionExtensions { public static IServiceCollection AddActors(this IServiceCollection s); }
```

**Usage.**
```csharp
var ralph = await actors.LoadAsync("ralph.bam", new() { ["walk"] = "ralph-walk.bam", ["wave"] = "ralph-wave.bam" });
ralph.Node.ReparentTo(scene.Root);
ralph.Anim("walk").Loop(true);                                  // native AnimInterface

// Half-body: wave with the arms while the legs keep walking. (Advanced rigging is on Rig.)
ralph.Rig.MakeSubpart("arms", new(IncludeJoints: ["arm_*", "hand_*"], ExcludeJoints: []));
ralph.Anim("wave", part: "arms").Play();

// In a cutscene (08): the walk holds its slot, poses from t — scrub/reverse-correct.
var scene1 = new Sequence(
    new Parallel(
        ralph.Node.PosTo(doorstep, 3, Ease.InOut),
        new ActorInterval(ralph, "walk", loop: true, duration: 3)),
    ralph.CrossFade("walk", "idle", 0.4));
```

**Design notes.**
- **Why `IActor` passes the wrap-rule bar.** It composes several natives (Loader + `Character` + per-part `AnimControlCollection` + `PartSubset` configuration) and holds real state (the name→part→control map, subpart definitions applied to every later bind). What it deliberately does **not** do is re-expose playback or blending semantics: `Anim(...)` returns `IAnimControl` (the `AnimInterface` surface, used directly, or `TryAnim` for a non-throwing lookup), and the raw native surface — `Rig.Part(...)` (the `PartBundle`), subparts, joints, LOD — hangs off `IActor.Rig` (an `IActorRig`) rather than crowding the everyday actor. The old `IAnimationController` re-wrap is deleted.
- **Binding is eager and synchronous (v1).** Named anim files bind at load time, not lazily: `PartBundle.load_bind_anim(loader, file, flags, subset, allow_async: false)` then `AnimControl.wait_pending()` and a `has_anim()` check, so a missing/mismatched clip fails fast during `Load`. The model itself loads through `Loader.LoadSync`, and `LoadAsync` currently wraps that synchronous load in a completed `PandaTask<IActor>`. The lazy/async path (`allow_async: true`, `AnimControl.is_pending`/`wait_pending`/`set_pending_done_event`, the loader's awaitable `AsyncFuture`) is published in the bindings but not yet used here.
- **`ActorInterval` is pose-per-step** (verified against Python's `privStep`): `frame = startFrame ± t·frameRate` (modulo the range when `constrainedLoop`), then `control.Pose(frame)` — the animation is *driven by the timeline's clock*, not free-running. That is what makes pause/scrub/reverse of a cutscene move the character correctly. `loop` wraps the whole animation; `constrainedLoop` wraps within `startFrame..endFrame`; if `duration` exceeds the range without looping, the final frame holds.
- **Cross-fades are native.** `CrossFade` builds a `CLerpAnimEffectInterval` (`add_control(control, name, beginEffect, endEffect)`), lerping the *from* anim's weight 1→0 and the *to* anim's 0→1 over the window at C++ speed — enable blending on the part first. Weights land exactly where `SetBlendWeight` would put them, so timeline blends and manual blends compose.
- **Geometry LOD rides one merged skeleton.** A LOD actor parents each level's parts under a `LODNode` (`add_switch(in, out)` per level), and — following Python's default `merge-lod-bundles` — merges every level's `PartBundle` into one common handle via `Character.merge_bundles`. Consequence: **animations bind once and drive all LOD levels in sync**; `Anim`/`SetBlendWeight`/joints are LOD-agnostic. Switching is the native `LODNode` surface (via `Rig.LodNode`) used directly (`ForceSwitch` for debugging a level, `SetLodScale`, `SetCenter`); v1 is merged-only (the non-merged per-LOD-bundle mode adds bookkeeping for no known need).
- **No animation events — honestly.** The engine raises no per-frame or anim-done events, and `direct` never had them either. Frame-triggered gameplay ("spawn the hit at frame 12") is a `Sequence(new ActorInterval(a, "swing", endFrame: 12), new Call(Hit), new ActorInterval(a, "swing", startFrame: 12))` or a gameplay task polling `Anim("swing").GetFrame()`. Nothing observable is fabricated here.
- **Cleanup is ordinary.** `Character` is a plain refcounted node — no Python-`Actor.cleanup()` trap to replicate. `IActor.Dispose` releases controlled joints and detaches; dropping the node is otherwise enough.

**Non-features (v1).** Non-merged LOD bundles (per-level independent binds — Python's `merge-lod-bundles false` mode) deferred; merged is the default and the sane semantics. Morph/blend-shape slider helpers (`controlJoint` on sliders) deferred. Egg-syntax anim baking/tooling out of scope ([00](00-overview.md) §8).

**Open items.**
- (none)

> **Verified:** `ActorLoader.HierarchyFlags` maps `LooseHierarchy` to `HmfOkAnimExtra | HmfOkPartExtra | HmfOkWrongRootName`. `ActorTests` load the Roaming Ralph sample, call inherited `AnimInterface` methods on `IAnimControl` (`Loop`, `Stop`, frame queries), verify blend weights and `CLerpAnimEffectInterval` cross-fades, and render Ralph/LOD Ralph offscreen to prove poses affect pixels.

> **Verified (1.11 headers + Actor.py):** `AnimInterface` PUBLISHED: `play()`/`play(from,to)`, `loop(restart[,from,to])`, `pingpong`, `stop`, `pose(frame)`, `set_play_rate`/`get_play_rate`, `get_frame_rate`/`get_num_frames`/`get_frame`/`get_full_frame`/`get_frac`, `is_playing`, `get_play_mode`; `AnimControl` adds `is_pending`/`wait_pending`/`set_pending_done_event`, `get_part`/`get_anim`. `PartBundle` PUBLISHED: `set_blend_type` (+`BlendType`), `set_anim_blend_flag`, `set_frame_blend_flag`, `set_control_effect(control, effect)`/`clear_control_effects`, `bind_anim(anim, flags, subset)`, `load_bind_anim(loader, filename, flags, subset, allow_async)`, `control_joint(name, node)`, `freeze_joint`, `release_joint`. `PartSubset` PUBLISHED: `add_include_joint(GlobPattern)`/`add_exclude_joint`. `auto_bind(root, AnimControlCollection, flags)` exposed. `Character` PUBLISHED: `get_bundle(i)`, `merge_bundles(old_handle, other_handle)`, `set_lod_animation(center, far, near, delay_factor)`. `LODNode` PUBLISHED: `add_switch(in, out)`, `force_switch(index)`/`clear_force_switch`, `set_lod_scale`, `set_center`, `make_default_lod`. Python `Actor.mergeLODBundles` defaults **true** (`merge-lod-bundles`): all levels' bundles merge into common handles, so one bind drives every LOD in sync. `CLerpAnimEffectInterval` PUBLISHED (panda3d.direct): ctor `(name, duration, blend_type)` + `add_control(control, name, begin_effect, end_effect)`. Python `Actor.exposeJoint` = `CharacterJoint.add_net_transform(node)` (or `add_local_transform`); `Actor.controlJoint` = `bundle.control_joint(jointName, node)`; `ActorInterval.privStep` = `frame = start ± t·frameRate` (mod range if constrained) → `control.pose(frame)` per bound control.

**See also.** [08 Intervals](08-intervals.md) (`ActorInterval` holds its slot; `CrossFade` composes into timelines); [01 Abstractions](01-abstractions.md) (`ISceneManager` roots; wrap rule); [02 Hosting](02-hosting.md) (async loading rides the fork's awaitable `AsyncFuture`).
