# Panda3D.Framework.Physics

Panda's collision system exposed as an **explicit traverse** plus **typed C# observables** of the engine's own `CollisionEntry` objects — instead of `direct`'s `"%fn-into-%in"` string-event callbacks. It is the collision layer of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework).

Solids, masks, and handlers stay the **native** Panda types (`CollisionNode`, `CollisionSphere`/`Ray`/`Box`/…, `BitMask32`, `CollisionHandlerPusher`/`Floor`/`Gravity`); this package adds the world, the observables, and query/ray helpers.

## Provides

- `AddCollision(autoTraverse = true)` — registers `ICollisionWorld`, the `resetPrevTransform` task (fluid-motion bookkeeping), and, by default, the per-frame traverse of the scene root at the collision slot.
- `ICollisionWorld` — `Add`/`Remove` a from-collider; `AddPusher`/`AddFloor`/`AddGravity` for physical responses (observables still fire); `AddQuery`, `Raycast`, `Pick`; `Traverse`; `Entered`/`Again`/`Exited` observables; `ShowColliders`/`HideColliders`; native `Traverser`/`Handler` escape hatches.
- `CollisionEntryStreams` — `.By(...)` / `.Into(...)` filters over the streams, matching a `NodePath` or a node name (the observable answer to `%fn`/`%in`).
- `ICollisionQuery` — a persistent query on the shared traverse: `Hits`, `Nearest`, `NearestInto(name)`.
- `RaycastHit` — one-shot ray/pick result (`Into`, `SurfacePoint`, `SurfaceNormal`, `Distance`).
- `StripToCollision()` — reduce a loaded model to just its `CollisionNode`s in place.

```csharp
services.AddCollision();
// ...
var world = sp.GetRequiredService<ICollisionWorld>();

// Register a from-collider (its node is a native CollisionNode).
world.Add(player);
world.Entered.Into("hazard").Subscribe(e => TakeDamage(e.GetSurfacePoint(scene.Root)));

// Physical response and observables together — the pusher still feeds the streams.
var pusher = world.AddPusher(player, ralph.Node);

// One-shot pick ray through a camera.
RaycastHit? hit = world.Pick(camera, mouseX, mouseY, scene.Root);
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
