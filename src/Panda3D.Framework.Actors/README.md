# Panda3D.Framework.Actors

Loading and controlling animated models over Panda's native `Character`/`AnimControl`/`PartBundle` — the composition Python's `Actor` provided (name→control maps, subparts, joint rigging, LOD), without re-wrapping playback. It is the actor/animation layer of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework).

Playback and blending stay native: `Anim(...)` returns the engine's `AnimControl` handle, `Rig.Part(...)` the native `PartBundle`.

## Provides

- `AddActors()` — registers `IActorLoader`.
- `IActorLoader` — `Load`/`LoadAsync` for a single model + named anims, a multipart part map, or a full `ActorDefinition` (parts × LODs).
- `IActor` — `Node`, `Character`, `Anim`/`TryAnim` (returns native `IAnimControl`), `Anims`, and blending (`EnableBlend`/`DisableBlend`/`SetBlendWeight`); advanced rigging behind `Rig`.
- `IActorRig` (via `IActor.Rig`) — subparts (`MakeSubpart`), raw `Part` bundles, joint `ExposeJoint`/`ControlJoint`/`FreezeJoint`/`ReleaseJoint`, and LOD (`Lods`, `LodNode`, `SetAnimRateLod`).
- `ActorInterval` — poses an animation from the timeline's time, so it composes into `Sequence`/`Parallel` and scrubs/reverses deterministically.
- `ActorTimelines.CrossFade(...)` — native weight cross-fade between two clips (`CLerpAnimEffectInterval`).
- Supporting types: `ActorOptions`, `ActorPart`, `ActorDefinition`, `ActorPartDef`, `LodLevel`, `SubpartDef`, `AnimClip`.

```csharp
services.AddActors();
// ...
var ralph = await actors.LoadAsync("ralph.bam",
    new Dictionary<string, string> { ["walk"] = "ralph-walk.bam", ["wave"] = "ralph-wave.bam" });
ralph.Node.ReparentTo(scene.Root);
ralph.Anim("walk").Loop(true);                 // native AnimControl handle

// Half-body: wave with the arms while the legs keep walking.
ralph.Rig.MakeSubpart("arms", new(["arm_*", "hand_*"], []));
ralph.Anim("wave", part: "arms").Play();

// In a cutscene: poses from timeline time, so scrub/reverse work.
new ActorInterval(ralph, "walk", loop: true, duration: 3);
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
