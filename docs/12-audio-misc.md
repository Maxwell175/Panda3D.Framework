# 12 — Audio & Misc (`Panda3D.Framework.Audio`)

**Purpose.** Audio, on the engine's own types: sounds and managers are the **native** `AudioSound`/`AudioManager` binding classes used directly — the same objects [08](08-intervals.md)'s `SoundInterval` seeks and [10](10-gui.md)'s widget sounds consume. The framework adds only what Panda leaves to Python: **lifecycle** (create the SFX/music managers, run the per-frame `update()` at the audio slot), the **3-D positional layer** (`Audio3DManager` is Python-only — an attach registry pushing node positions into the published 3-D primitives each frame), and a thin reusable **`ISound`** handle over each native sound whose **`Finished`** is awaitable (`set_finished_event` through the pump, mirroring interval done-events). This doc also records the deliberate omissions: no blackboard, no FSM (deferred, below).

**Replaces in `direct`.** ShowBase's audio setup (`createBaseAudioManagers`, `sfxManagerList`/`musicManager`, `loadSfx`/`loadMusic`, `playSfx`/`playMusic`, the audio update task) and `direct.showbase.Audio3DManager`. The `bboard`/BulletinBoard global is **not** replaced — it's cut (see Non-features).

**Dependencies.** `Abstractions`; `Events` (finished-events ride the pump); `Scheduling` (the update task at `FrameSlots.Audio`); the fork's C# bindings (core — `AudioManager`/`AudioSound`).

**Public surface.**
```csharp
public interface IAudio : IDisposable {                 // dispose shuts both managers down
    AudioManager Sfx { get; }                           // NATIVE managers, used directly:
    AudioManager Music { get; }                         //   get_sound / set_volume / set_active / concurrent limit
    ISound LoadSfx(string path, bool positional = false);    // load once -> reusable handle (positional: true for 3-D use)
    ISound LoadMusic(string path);
    ISound Wrap(AudioSound sound);                      // lift a native sound (GUI/manager/null sound) into the ISound surface
}

public interface ISound {                               // thin managed handle over one native AudioSound
    void Play();                                        // (re)start from the beginning
    void Stop();
    bool IsPlaying { get; }
    float Volume { get; set; }                          // [0, 1]
    bool Loop { get; set; }
    IObservable<Unit> Finished { get; }                 // fires once on completion then completes; await sound.Finished
    AudioSound Native { get; }                          // escape hatch: rate, balance, seek, finished-event names
}

public interface IAudio3D : IDisposable {               // the Audio3DManager equivalent — one per (manager, listener)
    void AttachListener(NodePath node);                 // usually the view's camera rig node
    void Attach(AudioSound sound, NodePath emitter);    // per-frame: emitter net-position/velocity -> set_3d_attributes
    void Detach(AudioSound sound);
    void SetVelocity(AudioSound sound, LVector3f velocity);   // explicit velocity (doppler)
    void SetVelocityAuto(AudioSound sound);             // derive from frame-over-frame position delta
    float DistanceFactor { get; set; }                  // audio_3d_set_distance_factor (units-per-meter)
}

public static class AudioServiceCollectionExtensions {
    public static IServiceCollection AddAudio(this IServiceCollection s);   // Sfx+Music managers + update task (FrameSlots.Audio)
    public static IServiceCollection AddAudio3D(this IServiceCollection s); // per-view IAudio3D over the Sfx manager
}
```

**Usage.**
```csharp
var click = audio.LoadSfx("click.ogg");
click.Play();                                            // ISound: Play/Stop, Volume, Loop; Native for rate/balance/seek

var theme = audio.LoadMusic("theme.ogg");
theme.Loop = true; theme.Play();
audio.Music.SetVolume(0.6f);                             // volume group = the native manager's volume

// 3-D: footsteps follow the actor; the camera listens. Attach the native sound (ISound.Native).
var steps = audio.LoadSfx("steps.ogg", positional: true);
audio3d.AttachListener(view.Camera.Node);
audio3d.Attach(steps.Native, ralph.Node);
audio3d.SetVelocityAuto(steps.Native);

await stinger.Finished;                                  // ISound.Finished — coroutine-friendly, like intervals' WhenDone
```

**Design notes.**
- **Managers stay native; sounds get a thin handle.** `AudioManager` is used directly — no wrapper — because it already publishes its whole working surface (`get_sound(file, positional, mode)`, `set_volume`, `set_active`, `set_concurrent_sound_limit`, `update`). `AudioSound` likewise publishes `play`/`stop`, `set_loop`/`loop_count`/`loop_start`, **`set_time`** — the seek [08](08-intervals.md)'s `SoundInterval` re-syncs with — `set_volume`/`set_balance`/`set_play_rate`, `set_active`, `length`, `status`, `set_finished_event`, and stays reachable as `ISound.Native`. `ISound` is not a re-wrap of that surface: it is a deliberately thin managed handle adding only what a raw `AudioSound` can't express in C# idiom — a reusable `Play`/`Stop`/`Volume`/`Loop` surface and, above all, the **awaitable `Finished`** (`IObservable<Unit>`). `LoadSfx`/`LoadMusic` hand one back; `Wrap` lifts any native sound (a GUI sound, a manager sound) into it. `IAudio` itself is *lifecycle + loading*, and `IDisposable` — disposing it shuts both managers down.
- **The update task is the `audioLoop`.** `AddAudio` creates the two managers via `AudioManager.CreateAudioManager()` and registers one frame task at `FrameSlots.Audio` (60) calling `update()` on each — ShowBase's audio loop, made explicit. Extra volume groups (ambient, voice): create another native manager and register its `update()` as your own task; nothing framework-specific about it.
- **`IAudio3D` passes the wrap-rule bar** the same way `IActor` and `ParticleEffect` did: the C++ side publishes only primitives (`AudioSound.set_3d_attributes(px..vz)`, `AudioManager.audio_3d_set_listener_attributes(...)`, `audio_3d_set_distance_factor`), and the attach-registry-plus-per-frame-push layer is Python-only (`Audio3DManager`: `attachSoundToObject`/`attachListener`/`setSoundVelocity(Auto)`/`update`). Ours holds the sound→emitter map, pushes net positions (and derived or explicit velocities) each frame from the audio task, and detaches everything on dispose. Load positional sounds with `positional: true`, per the manager's `get_sound` contract.
- **Finished is awaitable, exactly like intervals.** `ISound.Finished` is an `IObservable<Unit>` built lazily on first access: internally the handle assigns a unique `set_finished_event("snd-done-{id}")`, the engine queues it on completion, the [Events](06-events.md) pump routes it, and the handle relays it onto a replaying `AsyncSubject` — so `await sound.Finished` completes once playback ends (an already-stopped sound completes immediately; a looping one never does). A stinger-then-resume-music sequence is two awaits — or use [08](08-intervals.md)'s `SoundInterval` when it should hold a *timeline* slot instead.
- **Logging.** `DirectNotify` is not reimplemented; `ILogger<T>` throughout, as everywhere else.

**Non-features (v1).** **No blackboard** — `direct`'s `bboard` was a global string→object dictionary, and a framework `IBlackboard` would be the same opinion with DI paint: plain injected services (or the game's own store) are clearer, typed, and scoped for free — the same reasoning that cut `IWorld` and the typed event bus. No DSP/filter surface (`FilterProperties` is native if a backend supports it). No framework mixer/bus graph — the native per-manager volume *is* the group model.

**Open items.**
- (none)

> **Verified:** The local build config and runtime package include OpenAL audio (`audio-library-name p3openal_audio`, `libp3openal_audio.so`). FMOD/Miles are not shipped in the current linux-x64 runtime package. `AudioTests` cover audio-loop updates, awaitable finished events, and `Audio3D` attach/velocity updates.

---

## Deferred: finite state machines (excluded from v1)

**Decision.** No FSM ships in v1. Nothing in v1 depends on one, so this is a clean omission, not a stub. Revisit after v1.

**Why deferred rather than built.** The C# ecosystem already has mature, well-maintained transition-state-machine libraries — **Stateless** (`dotnet-state-machine/stateless`, Apache-2.0, targets .NET Standard 2.0 / .NET 8–10, the de-facto standard) is the obvious choice, with Appccelerate.StateMachine and LiquidState as alternatives. They cover `OnEntry`/`OnExit`, guard clauses, hierarchical substates, and async triggers, and they compose fine under DI (construct one in a factory, reference it by its own type). Stateless's async model is single-threaded, which fits the single-`"default"`-chain affinity model rather than fighting it. Building a general-purpose transition engine would be reinventing this — the same reasoning that led us to use `ILogger` instead of reimplementing `DirectNotify`.

**The only game-specific gap.** All of those libraries are trigger/event-driven and deliberately have **no per-frame tick**; they model transition *rules*, not "update the current state every frame." Game-agent FSMs (the half of `direct.fsm.FSM` that mattered) want an `OnUpdate(dt)` loop — scan-for-player while patrolling, re-path while chasing. That ticker is small.

**How to add it back later.** Two options, smallest first:
1. **Recommend/integrate Stateless** for transition-logic state machines (game-session lifecycle, connection/login flow, menu/screen flow, quest progression) — likely an optional integration package, not a hard core dependency.
2. **Add a thin per-frame state ticker** as the only first-party FSM surface — `IState { OnEnter(); OnUpdate(float dt); OnExit(); }` driven from a frame task ([Scheduling](07-scheduling-and-time.md)), with `dt` from `IGameClock` and state-change notifications as `IObservable<T>` on the ticker ([06](06-events.md)).

**Caveat for whoever revisits this.** For complex NPC behavior, FSMs are commonly superseded by behavior trees, utility AI, or GOAP. Keep any future FSM minimal and let games reach for a behavior-tree library if they outgrow states — don't grow this into a large AI framework.

> **Verified (1.11 headers + sources):** `AudioSound` PUBLISHED (pure-virtual interface): `play`/`stop`, `set_loop`/`set_loop_count`/`set_loop_start`, `set_time(start_time)`/`get_time`, `set_volume`/`set_balance`/`set_play_rate`, `set_active`, `set_finished_event(string)`/`get_finished_event`, `length`, `status`, `is_positional`, `set_3d_attributes(px, py, pz, vx, vy, vz)`. `AudioManager` PUBLISHED: static `create_AudioManager()`, `get_sound(filename, positional, mode)` (+ `MovieAudio` overload), `get_null_sound`, `set_volume`, `set_active`, `set_concurrent_sound_limit`, `update()`, `audio_3d_set_listener_attributes(px..vz, fx..fz, ux..uz)`, `audio_3d_set_distance_factor`. ShowBase keeps a `sfxManagerList` **plus** a separate `musicManager` (`createBaseAudioManagers`) — the Sfx/Music split mirrored here. `Audio3DManager.py` is Python-only: `attachSoundToObject`/`detachSound`/`attachListener`/`detachListener`/`setSoundVelocity`/`setSoundVelocityAuto`/`update(task)` — the composition `IAudio3D` provides.

**See also.** [08 Intervals](08-intervals.md) (`SoundInterval` holds a timeline slot; seeks via `set_time`); [10 GUI](10-gui.md) (widget sounds take `AudioSound`); [06 Events](06-events.md) (finished-events ride the pump); [07 Scheduling & Time](07-scheduling-and-time.md) (`FrameSlots.Audio`).
