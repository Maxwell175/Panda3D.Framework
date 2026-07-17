# 11 — Physics, Collision & Particles (`Panda3D.Framework.Physics`)

**Purpose.** Panda's collision system with an **explicit traverse** and — the critical requirement — **typed C# observables instead of string-event callbacks**: consumers see `IObservable<CollisionEntry>` carrying the engine's own entry objects, never a `"%fn-into-%in"` pattern string. Plus the built-in physics managers (forces/integrators — what particles ride on) and the **particle system** surface that provides the `ParticleInterval` timeline adapter. Heavyweight rigid-body physics is deliberately *not* abstracted: Panda's Bullet bindings are `PUBLISHED` — use them directly (see Design notes).

**Replaces in `direct`.** The collision system's `direct` face — `base.cTrav` magic auto-traverse and the `messenger`-pattern collision handlers (`"%fn-into-%in"` string substitution) — plus `enableParticles()`'s lazy global managers and the Python-only `ParticleEffect` layer.

**Dependencies.** `Abstractions`; `Events` (the observables ride the pump); `Scheduling` (traverse/update tasks, `AddFixedStep`); `Intervals` for `ParticleInterval`; the fork's C# bindings — collision is in core; particles/built-in physics are the **`panda3d.physics`** module (`p3physics` + `p3particlesystem`); Bullet is the **`panda3d.bullet`** module.

## Collision: the engine tracks state, C# sees observables

The mechanism, verified end-to-end: `CollisionHandlerEvent` already does the in/again/out **state tracking** in C++ (comparing this pass against the last), and for each transition it calls `throw_event(name, EventParameter(entry))` — **the native `CollisionEntry` is the event's typed payload.** `direct` obscures this behind name substitution (`add_in_pattern("%fn-into-%in")` encodes the colliding pair into the event *name*, then string-matches in Python). We invert that: give the handler **constant** pattern names (`set_in_pattern("colw-{worldId}-in")`, same for again/out), let every transition arrive on one name per kind, and read the pair from the *entry* — `GetFromNodePath()`/`GetIntoNodePath()`, surface point/normal — through the [Events](06-events.md) pump into `Subject`s. Strings remain a pump transport detail (exactly like window events); the public surface is observables of native entries.

**Public surface.**
```csharp
public interface ICollisionWorld : IDisposable {
    // Register a collider (a NodePath whose node is a CollisionNode). Default handler = this world's
    // event handler (feeds the observables). Pass a native handler (queue/pusher/floor/gravity) to
    // add a physical response — they derive from CollisionHandlerEvent, so the observables still fire.
    void Add(NodePath collider, CollisionHandler? handler = null);
    void Remove(NodePath collider);
    void Traverse(NodePath root);                      // explicit; AddCollision registers it at FrameSlots.Collision

    IObservable<CollisionEntry> Entered { get; }       // native CollisionEntry payloads
    IObservable<CollisionEntry> Again { get; }         // still colliding this pass
    IObservable<CollisionEntry> Exited { get; }
    // Convenience filters — the full explicit matrix: {Entered, Again, Exited} x {By = from side, Into = into side}
    // x {NodePath = that exact node, string = node name (model-authored colliders)}. All are Where-sugar over the
    // three streams above; this matrix is the observable answer to direct's "%fn"/"%in" name substitution.
    IObservable<CollisionEntry> EnteredBy(NodePath from);
    IObservable<CollisionEntry> EnteredBy(string fromName);
    IObservable<CollisionEntry> EnteredInto(NodePath into);
    IObservable<CollisionEntry> EnteredInto(string intoName);
    IObservable<CollisionEntry> AgainBy(NodePath from);
    IObservable<CollisionEntry> AgainBy(string fromName);
    IObservable<CollisionEntry> AgainInto(NodePath into);
    IObservable<CollisionEntry> AgainInto(string intoName);
    IObservable<CollisionEntry> ExitedBy(NodePath from);
    IObservable<CollisionEntry> ExitedBy(string fromName);
    IObservable<CollisionEntry> ExitedInto(NodePath into);
    IObservable<CollisionEntry> ExitedInto(string intoName);

    CollisionTraverser Traverser { get; }              // native escape hatches
    CollisionHandlerEvent EventHandler { get; }
}

// Built-in physics + particles (one service: particles ride the physics managers, as enableParticles did)
public interface IParticles : IDisposable {
    ParticleEffect Create(string name);
    PhysicsManager Physics { get; }                    // native: add ActorNodes/forces (jetpack-style impulses)
    ParticleSystemManager Manager { get; }             // native
}
public sealed class ParticleEffect : IDisposable {     // the C# equivalent of direct's Python-only ParticleEffect
    public NodePath Node { get; }                      // parent/position like any node
    public IList<ParticleSystem> Systems { get; }      // native systems; configure factory/renderer/emitter natively
    public void SoftStart();                            // ParticleSystem.soft_start on each system
    public void SoftStop();                             // emission off; live particles drain
}
public sealed class ParticleInterval : ManagedInterval {
    public ParticleInterval(ParticleEffect effect, NodePath parent, double duration, double softStopT = 0, bool cleanup = false);
}

public static class PhysicsServiceCollectionExtensions {
    public static IServiceCollection AddCollision(this IServiceCollection s);  // world + traverse task (FrameSlots.Collision) + resetPrevTransform (FrameSlots.PrevTransform)
    public static IServiceCollection AddParticles(this IServiceCollection s);  // managers + update task (do_particles/do_physics per frame)
}
```

**Usage.**
```csharp
// Solids, nodes, and masks are the NATIVE types: CollisionNode, CollisionSphere/Ray/Box/…, BitMask32.
var cnode = new CollisionNode("player");
cnode.AddSolid(new CollisionSphere(0, 0, 0, 1));
cnode.SetFromCollideMask(BitMask32.Bit(0));
var player = ralph.Node.AttachNewNode(cnode);

collision.Add(player);                                          // event handler → observables
subs.Add(collision.EnteredBy(player)                            // subs: Rx CompositeDisposable
    .Where(e => e.GetIntoNodePath().HasNetTag("hazard"))
    .Subscribe(e => TakeDamage(e.GetSurfacePoint(scene.Root))));

// Physical response + observables together: pusher derives from the event handler.
var pusher = new CollisionHandlerPusher(); pusher.AddCollider(player, ralph.Node);
collision.Add(player, pusher);

// Model-authored colliders: the level's egg/gltf defines <Collide> groups -> named CollisionNodes.
// Into-side geometry needs NO registration — only from-colliders join the traverser.
var level = await loader.LoadModelAsync("level.bam");           // native Loader: requests are awaitable futures
level.ReparentTo(scene.Root);
subs.Add(collision.EnteredInto("trigger_door").Subscribe(_ => OpenDoor()));
subs.Add(collision.ExitedInto("trigger_door").Subscribe(_ => CloseDoor()));
```

Particles are ordinary native `ParticleSystem`s collected under a small framework root:
```csharp
using var effect = particles.Create("sparks");
effect.Node.ReparentTo(scene.Root);

var system = new ParticleSystem(256);
system.SetBirthRate(0.01f);
system.SetLitterSize(64);
system.SetFactory(new PointParticleFactory());
system.SetRenderer(new PointParticleRenderer());
system.SetEmitter(new PointEmitter());

effect.Systems.Add(system);     // attaches to PhysicsManager + ParticleSystemManager
effect.SoftStart();

var timed = new ParticleInterval(effect, scene.Root, duration: 2.0, softStopT: 0.5, cleanup: true);
intervals.Play(timed);
```

**Design notes.**
- **No framework entry/mask/solid types.** The old draft's `CollisionEntry` record literally name-collided with Panda's own `CollisionEntry` — the tell. Observables carry the native `CollisionEntry` (from/into solids, nodes, and NodePaths; surface point/normal; interior point — richer than any projection); masks are `BitMask32`; solids are the native `CollisionSphere`/`Ray`/`Segment`/`Box`/`Capsule`/`Polygon` ctors; `CollisionNode` is constructed directly (the `Add` helper takes the finished NodePath). Prefer-the-engine, throughout.
- **Model-authored colliders are half the workflow.** Level geometry and triggers are typically authored *in the model*: egg `<Collide>` groups load as `CollisionNode`s **named after their group** (verified in `eggLoader`), already inside the loaded tree. Two consequences. First, **into-side objects need no registration** — `ICollisionWorld.Add` registers *from*-colliders with the traverser; authored walls/floors/triggers participate purely by collide mask. Second, their identity is the **node name** (plus any tags): the convenience matrix — `{Entered, Again, Exited} × {By, Into} × {node, name}` — is the observable answer to `direct`'s `%fn`/`%in` substitution. `NodePath` overloads match that exact node; `string` overloads match the node's name; all are stated sugar over `Where`, which still covers globs, tags (`GetIntoNodePath().GetNetTag`), and anything fancier. Post-load adjustment (masks, visualizing) finds them with `level.FindAllMatches("**/+CollisionNode")`.
- **Physical responses are the native handlers, and events survive them.** `CollisionHandlerPusher` (wall sliding), `CollisionHandlerFloor`/`Gravity` (ground clamping), `CollisionHandlerFluidPusher` all derive from `CollisionHandlerPhysical : CollisionHandlerEvent` — so a collider registered with a pusher **still feeds the observables** (the world stamps its constant patterns onto the handler you pass). Response composition = pick the native handler; no framework response layer.
- **Polling still exists.** For pick-ray queries and oneshot checks, use a native `CollisionHandlerQueue` (`SortEntries`/`Entries`) with a manual `Traverser.Traverse(root)` — the classic mouse-picking recipe (`CollisionRay.SetFromLens(camNode, mouse)` → queue → nearest entry) works unchanged and needs no observables.
- **Explicit traverse, scheduled.** `AddCollision` registers `Traverse(scene.Root)` at `FrameSlots.Collision` (30, the `collisionLoop` slot) **and** the `resetPrevTransform` task at `FrameSlots.PrevTransform` (−51, `PandaNode.ResetAllPrevTransforms()`) — required for fluid movement (`set_fluid_pos` in pushers and [08](08-intervals.md)'s fluid lerps) to see frame-over-frame motion; `direct` runs the same task. Nothing traverses because an attribute was assigned (`base.cTrav` is gone). Per-zone servers: one `ICollisionWorld` per zone scope, traversing that zone's root — the observables are per-world, so zone events don't cross.
- **Rigid-body physics: use Bullet's bindings directly.** A keyed `IPhysicsWorld` abstraction over Bullet/legacy/external would tax every boundary with conversions and opinions — the same reasoning that killed the typed event bus and `IWorld`. Panda's Bullet module is `PUBLISHED` (`BulletWorld`, bodies, shapes); use it as-is and step it deterministically from `IFrameScheduler.AddFixedStep(hz, dt => bulletWorld.DoPhysics(dt))` ([07](07-scheduling-and-time.md)) — fixed-step for determinism, especially server-side. The framework's only role is that recipe.
- **Particles ride the built-in physics managers.** `AddParticles` registers a `PhysicsManager` + `ParticleSystemManager` pair and one frame task calling `DoPhysics(dt)`/`DoParticles(dt)` — what `enableParticles()` did lazily via globals. A `ParticleEffect` is a node plus native `ParticleSystem`s (each configured with its factory/renderer/emitter natively); `SoftStart`/`SoftStop` map to the systems' published `soft_start`/`soft_stop`, which is exactly what [08](08-intervals.md)'s `ParticleInterval` drives over a timeline window. The same `Physics` manager also serves non-particle force play (`ActorNode` + `LinearVectorForce` impulses).
- **`.ptf` files are not portable — by construction.** `direct`'s `ParticleEffect.loadConfig` literally `exec()`s the file: a `.ptf` **is a Python script**, not data. So there is no `.ptf` loader here; effects are configured in code (the natives are all published), and serialization, if wanted, is the game's own format. Stated so nobody hunts for a missing feature.

**Non-features (v1).** No physics-engine abstraction (`IPhysicsWorld`/`IRigidBody`/keyed backends — Bullet natively instead); no `.ptf` loading; no framework force/response DSL (native forces and handlers are the surface).

**Open items.**
- (none)

> **Verified:** `CollisionWorldTests` cover typed collision entry observables and filters. `ParticlesTests` cover manager updates, `ParticleEffect` ownership, visible rendered particles, and `ParticleInterval` parent/soft-stop/cleanup behavior.

> **Verified (1.11 headers + sources):** `CollisionHandlerEvent` PUBLISHED: `add/set/clear_in_pattern`, `add_again_pattern`, `add_out_pattern` (+seq getters); its `.cxx` throws `throw_event(event, EventParameter(entry))` — **the entry is the typed payload**. `CollisionEntry` PUBLISHED: `get_from`/`get_into` (solids), `get_from_node`/`get_into_node`, `get_from_node_path`/`get_into_node_path`, `get/has_surface_point`, `surface_normal`, `interior_point`. `CollisionTraverser` PUBLISHED: `add_collider(NodePath, handler)`, `remove_collider`, `traverse(root)`. `CollisionHandlerQueue` PUBLISHED: `sort_entries`, `clear_entries`, `get_entries` (seq). Physical handlers present: `CollisionHandlerPusher`/`Floor`/`Gravity`/`FluidPusher` (derive via `CollisionHandlerPhysical` from `CollisionHandlerEvent`). `ParticleSystem.soft_start(br, first_birth_delay)`/`soft_stop(br)` PUBLISHED. `direct`'s `ParticleEffect.loadConfig` = `exec(data)` — `.ptf` is executable Python, confirming no portable loader exists. Fork module list (CMake): `add_csharp_module(panda3d.bullet p3bullet)` under `HAVE_BULLET`, and `add_csharp_module(panda3d.physics p3physics p3particlesystem LINK pandaphysics)` — both the Bullet and physics/particles natives are generated C# modules. `CollisionHandlerEvent::throw_event_for(patterns, CollisionEntry*)` relays the entry into every thrown pattern event. `eggLoader.cxx`: egg `<Collide>` groups produce `new CollisionNode(egg_group->get_name())` + `make_collision_solids` — model-authored colliders arrive as named `CollisionNode`s.

**See also.** [06 Events](06-events.md) (the pump the collision observables ride); [07 Scheduling & Time](07-scheduling-and-time.md) (`FrameSlots.Collision`, `AddFixedStep` for Bullet); [08 Intervals](08-intervals.md) (`ParticleInterval` over `SoftStart`/`SoftStop`); [01 Abstractions](01-abstractions.md) (prefer-the-engine rule).
