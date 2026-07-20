# 11 — Physics & Collision (`Panda3D.Framework.Physics`)

**Purpose.** Panda's collision system with an **explicit traverse** and — the critical requirement — **typed C# observables instead of string-event callbacks**: consumers see `IObservable<CollisionEntry>` carrying the engine's own entry objects, never a `"%fn-into-%in"` pattern string. Heavyweight rigid-body physics is deliberately *not* abstracted: Panda's Bullet bindings are `PUBLISHED` — use them directly (see Design notes). Built-in particles and the legacy force/integrator managers are **not** in this package — they ship separately as **`Panda3D.Framework.Particles`** (`IParticles`/`ParticleEffect`/`ParticleInterval`/`AddParticles`); look there, not here.

**Replaces in `direct`.** The collision system's `direct` face — `base.cTrav` magic auto-traverse and the `messenger`-pattern collision handlers (`"%fn-into-%in"` string substitution). (`enableParticles()`'s lazy global managers and the Python-only `ParticleEffect` layer are replaced by the separate `Panda3D.Framework.Particles` package.)

**Dependencies.** `Abstractions`; `Events` (the observables ride the pump); `Scheduling` (traverse + `resetPrevTransform` tasks, `AddFixedStep` for Bullet); the fork's C# bindings — collision is in core; Bullet is the **`panda3d.bullet`** module. (No `Intervals` dependency — that's the Particles package, which owns `ParticleInterval`.)

## Collision: the engine tracks state, C# sees observables

The mechanism, verified end-to-end: `CollisionHandlerEvent` already does the in/again/out **state tracking** in C++ (comparing this pass against the last), and for each transition it calls `throw_event(name, EventParameter(entry))` — **the native `CollisionEntry` is the event's typed payload.** `direct` obscures this behind name substitution (`add_in_pattern("%fn-into-%in")` encodes the colliding pair into the event *name*, then string-matches in Python). We invert that: give the handler **constant** pattern names (`set_in_pattern("colw-{worldId}-in")`, same for again/out), let every transition arrive on one name per kind, and read the pair from the *entry* — `GetFromNodePath()`/`GetIntoNodePath()`, surface point/normal — through the [Events](06-events.md) pump into `Subject`s. Strings remain a pump transport detail (exactly like window events); the public surface is observables of native entries.

**Public surface.**
```csharp
public interface ICollisionWorld : IDisposable {
    // Register a from-collider (a NodePath whose node is a CollisionNode). Default handler = this world's
    // event handler (feeds the observables). Pass a native handler (queue/pusher/floor/gravity) to add a
    // physical response — the world stamps its constant patterns onto it, so the observables still fire.
    void Add(NodePath collider, CollisionHandler? handler = null);
    void Remove(NodePath collider);

    // Physical-response conveniences: build + register the native handler, return it to tune.
    CollisionHandlerPusher  AddPusher (NodePath collider, NodePath pushee);
    CollisionHandlerFloor   AddFloor  (NodePath collider, NodePath floored, float offset = 0f);
    CollisionHandlerGravity AddGravity(NodePath collider, NodePath gravitated, float gravity);

    // A persistent poll registered on the shared per-frame traverse (feeds no observables).
    ICollisionQuery AddQuery(NodePath collider);

    void Traverse(NodePath root);                      // explicit; AddCollision registers it at FrameSlots.Collision

    // One-shot ray queries — each runs its own traverse; nearest into-hit or null.
    RaycastHit? Raycast(LPoint3f origin, LVector3f direction, NodePath against, BitMask32? mask = null);
    RaycastHit? Pick(NodePath camera, float filmX, float filmY, NodePath against, BitMask32? mask = null);  // camera node = LensNode

    IObservable<CollisionEntry> Entered { get; }       // native CollisionEntry payloads — a new collision this pass
    IObservable<CollisionEntry> Again   { get; }       // still colliding this pass
    IObservable<CollisionEntry> Exited  { get; }       // a collision ended this pass

    void ShowColliders(NodePath root);                 // debug viz; throws in a lean/AOT build — guard with #if DEBUG
    void HideColliders();

    CollisionTraverser    Traverser { get; }           // native escape hatches
    CollisionHandlerEvent Handler   { get; }
}

// Stream filtering is composed onto Entered/Again/Exited via EXTENSION METHODS (not a method matrix) — the
// observable answer to direct's "%fn"/"%in" substitution. NodePath overload = that exact node; string = node name.
public static class CollisionEntryStreams {            // extension methods on IObservable<CollisionEntry>
    public static IObservable<CollisionEntry> By  (this IObservable<CollisionEntry> s, NodePath from);
    public static IObservable<CollisionEntry> By  (this IObservable<CollisionEntry> s, string   fromName);
    public static IObservable<CollisionEntry> Into(this IObservable<CollisionEntry> s, NodePath into);
    public static IObservable<CollisionEntry> Into(this IObservable<CollisionEntry> s, string   intoName);
}
// e.g. world.Entered.Into("trigger_door") ; world.Exited.By(playerNode) ; world.Again.By(a).Into(b)

// Result of AddQuery: a persistent poll of the shared traverse; native CollisionEntry hits, nearest-first.
public interface ICollisionQuery {
    IReadOnlyList<CollisionEntry> Hits { get; }        // this query's hits from the most recent traverse
    CollisionEntry? Nearest { get; }
    CollisionEntry? NearestInto(string intoName);
    CollisionHandlerQueue Native { get; }              // escape hatch — read the queue directly
}
public readonly record struct RaycastHit(NodePath Into, LPoint3f SurfacePoint, LVector3f SurfaceNormal, float Distance);

public static class CollisionModels {
    // Strip a loaded model down to just its CollisionNodes in place (every other node -> plain PandaNode).
    public static NodePath StripToCollision(this NodePath model);
}

public static class PhysicsServiceCollectionExtensions {
    // world + resetPrevTransform (FrameSlots.PrevTransform); when autoTraverse, also Traverse(scene.Root)
    // at FrameSlots.Collision. Pass false to call ICollisionWorld.Traverse from your own task instead.
    public static IServiceCollection AddCollision(this IServiceCollection s, bool autoTraverse = true);
}
```

(Particles/built-in physics live in the separate **`Panda3D.Framework.Particles`** package: `AddParticles()` registers an `IParticles` service — a native `PhysicsManager` + `ParticleSystemManager` pair updated per frame — and `ParticleEffect`/`ParticleInterval` sit there. See its `IParticles.cs`/`ParticleEffect.cs`.)

**Usage.**
```csharp
// Solids, nodes, and masks are the NATIVE types: CollisionNode, CollisionSphere/Ray/Box/…, BitMask32.
var cnode = new CollisionNode("player");
cnode.AddSolid(new CollisionSphere(0, 0, 0, 1));
cnode.SetFromCollideMask(BitMask32.Bit(0));
var player = ralph.Node.AttachNewNode(cnode);

collision.Add(player);                                          // event handler → observables
subs.Add(collision.Entered.By(player)                           // subs: Rx CompositeDisposable
    .Where(e => e.GetIntoNodePath().HasNetTag("hazard"))
    .Subscribe(e => TakeDamage(e.GetSurfacePoint(scene.Root))));

// Physical response + observables together: the pusher still feeds the streams.
var pusher = collision.AddPusher(player, ralph.Node);           // returns the CollisionHandlerPusher to tune
// (equivalently: collision.Add(player, new CollisionHandlerPusher()) after wiring its colliders)

// Model-authored colliders: the level's egg/gltf defines <Collide> groups -> named CollisionNodes.
// Into-side geometry needs NO registration — only from-colliders join the traverser.
var level = await loader.LoadModelAsync("level.bam");           // native Loader: requests are awaitable futures
level.ReparentTo(scene.Root);
subs.Add(collision.Entered.Into("trigger_door").Subscribe(_ => OpenDoor()));
subs.Add(collision.Exited.Into("trigger_door").Subscribe(_ => CloseDoor()));

// One-shot picking (no observables): cast through the camera, take the nearest hit.
RaycastHit? hit = collision.Pick(cam.Node, mouseX, mouseY, against: scene.Root);
if (hit is { } h) Select(h.Into);
```

Particles moved out. Built-in particles and the legacy force/integrator managers ship as the separate
**`Panda3D.Framework.Particles`** package — `AddParticles()`, `IParticles`, `ParticleEffect` (native
`ParticleSystem`s under a movable node), and `ParticleInterval` ([08](08-intervals.md)) — so nobody hunts for
them here.

**Design notes.**
- **No framework entry/mask/solid types.** The old draft's `CollisionEntry` record literally name-collided with Panda's own `CollisionEntry` — the tell. Observables carry the native `CollisionEntry` (from/into solids, nodes, and NodePaths; surface point/normal; interior point — richer than any projection); masks are `BitMask32`; solids are the native `CollisionSphere`/`Ray`/`Segment`/`Box`/`Capsule`/`Polygon` ctors; `CollisionNode` is constructed directly (the `Add` helper takes the finished NodePath). Prefer-the-engine, throughout.
- **Model-authored colliders are half the workflow.** Level geometry and triggers are typically authored *in the model*: egg `<Collide>` groups load as `CollisionNode`s **named after their group** (verified in `eggLoader`), already inside the loaded tree. Two consequences. First, **into-side objects need no registration** — `ICollisionWorld.Add` registers *from*-colliders with the traverser; authored walls/floors/triggers participate purely by collide mask. Second, their identity is the **node name** (plus any tags): the stream filters — `.By(...)`/`.Into(...)` (the `CollisionEntryStreams` extension methods) composed onto `Entered`/`Again`/`Exited` — are the observable answer to `direct`'s `%fn`/`%in` substitution. The `NodePath` overload matches that exact node; the `string` overload matches the node's name; both are stated sugar over `Where`, which still covers globs, tags (`GetIntoNodePath().GetNetTag`), and anything fancier. Post-load adjustment (masks, visualizing) finds them with `level.FindAllMatches("**/+CollisionNode")`.
- **Physical responses are the native handlers, and events survive them.** `CollisionHandlerPusher` (wall sliding), `CollisionHandlerFloor`/`Gravity` (ground clamping), `CollisionHandlerFluidPusher` all derive from `CollisionHandlerPhysical : CollisionHandlerEvent` — so a collider registered with a pusher **still feeds the observables** (`Add` stamps the world's constant patterns onto any handler you pass). Response composition = pick the native handler; no framework response layer. The `AddPusher`/`AddFloor`/`AddGravity` conveniences just build the handler, wire the collider/pushee, register it, and hand it back to tune.
- **Polling still exists.** Built in: `Raycast(origin, dir, against)` and `Pick(camera, filmX, filmY, against)` return a `RaycastHit?` (nearest into-hit, in the traversed root's space) from a one-shot self-contained traverse — the classic mouse-picking recipe (`Pick` uses `CollisionRay.SetFromLens`) with no observables. For a persistent per-frame poll on the shared traverse, `AddQuery(collider)` hands back an `ICollisionQuery` (`Hits`/`Nearest`/`NearestInto`) — cheaper than a `Raycast` per frame. The native `CollisionHandlerQueue` is still the escape hatch (`SortEntries`, and `Entries` is now an `IReadOnlyList<CollisionEntry>`), reached via `ICollisionQuery.Native`.
- **Explicit traverse, scheduled.** `AddCollision` registers `Traverse(scene.Root)` at `FrameSlots.Collision` (30, the `collisionLoop` slot) **and** the `resetPrevTransform` task at `FrameSlots.PrevTransform` (−51, `PandaNode.ResetAllPrevTransform()`) — required for fluid movement (`set_fluid_pos` in pushers and [08](08-intervals.md)'s fluid lerps) to see frame-over-frame motion; `direct` runs the same task. Pass `AddCollision(autoTraverse: false)` to keep the reset task but drive `ICollisionWorld.Traverse` from your own task. Nothing traverses because an attribute was assigned (`base.cTrav` is gone). Per-zone servers: one `ICollisionWorld` per zone scope, traversing that zone's root — the observables are per-world, so zone events don't cross.
- **Rigid-body physics: use Bullet's bindings directly.** A keyed `IPhysicsWorld` abstraction over Bullet/legacy/external would tax every boundary with conversions and opinions — the same reasoning that killed the typed event bus and `IWorld`. Panda's Bullet module is `PUBLISHED` (`BulletWorld`, bodies, shapes); use it as-is and step it deterministically from `IFrameScheduler.AddFixedStep(hz, dt => bulletWorld.DoPhysics(dt))` ([07](07-scheduling-and-time.md)) — fixed-step for determinism, especially server-side. The framework's only role is that recipe.
- **Reducing a model to its colliders.** `StripToCollision()` (extension on `NodePath`) rewrites a loaded model in place, keeping every `CollisionNode` and replacing all other nodes with plain `PandaNode`s (name/transform/children survive) — a server that only needs collision geometry loads the model and strips it.

**Non-features (v1).** No physics-engine abstraction (`IPhysicsWorld`/`IRigidBody`/keyed backends — Bullet natively instead); no framework collision-response DSL (the native handlers, exposed via `Add`/`AddPusher`/`AddFloor`/`AddGravity`, are the surface). Particles, `.ptf` handling, and the built-in force/integrator managers live in the separate `Panda3D.Framework.Particles` package.

**Open items.**
- (none)

> **Verified:** `CollisionWorldTests` cover typed collision entry observables and the `.By(...)`/`.Into(...)` stream filters, the `AddPusher`/`AddFloor`/`AddGravity` conveniences, and `Raycast`/`Pick` → `RaycastHit`. (`ParticleEffect`/`ParticleInterval` coverage lives with the separate Particles package.)

> **Verified (1.11 headers + sources):** `CollisionHandlerEvent` PUBLISHED: `add/set/clear_in_pattern`, `add_again_pattern`, `add_out_pattern` (+seq getters); its `.cxx` throws `throw_event(event, EventParameter(entry))` — **the entry is the typed payload**. `CollisionEntry` PUBLISHED: `get_from`/`get_into` (solids), `get_from_node`/`get_into_node`, `get_from_node_path`/`get_into_node_path`, `get/has_surface_point`, `surface_normal`, `interior_point`. `CollisionTraverser` PUBLISHED: `add_collider(NodePath, handler)`, `remove_collider`, `traverse(root)`, `show_collisions`/`hide_collisions`. `CollisionHandlerQueue` PUBLISHED: `sort_entries`, `clear_entries`, `get_entries` (seq → `Entries` as `IReadOnlyList<CollisionEntry>`). Physical handlers present: `CollisionHandlerPusher`/`Floor`/`Gravity`/`FluidPusher` (derive via `CollisionHandlerPhysical` from `CollisionHandlerEvent`). Fork module list (CMake): `add_csharp_module(panda3d.bullet p3bullet)` under `HAVE_BULLET` — the Bullet natives are a generated C# module (`add_csharp_module(panda3d.physics p3physics p3particlesystem LINK pandaphysics)` is generated too, but consumed by the separate Particles package). `CollisionHandlerEvent::throw_event_for(patterns, CollisionEntry*)` relays the entry into every thrown pattern event. `eggLoader.cxx`: egg `<Collide>` groups produce `new CollisionNode(egg_group->get_name())` + `make_collision_solids` — model-authored colliders arrive as named `CollisionNode`s.

**See also.** [06 Events](06-events.md) (the pump the collision observables ride); [07 Scheduling & Time](07-scheduling-and-time.md) (`FrameSlots.Collision`, `AddFixedStep` for Bullet); [08 Intervals](08-intervals.md) (timelines; `ParticleInterval`, in the Particles package, composes there); [01 Abstractions](01-abstractions.md) (prefer-the-engine rule).
