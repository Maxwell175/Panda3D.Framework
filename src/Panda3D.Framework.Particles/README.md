# Panda3D.Framework.Particles

The particle subsystem for the Panda3D.Framework game framework: Panda's built-in
`PhysicsManager` and `ParticleSystemManager` exposed explicitly, with a managed
`ParticleEffect` and a timeline `ParticleInterval` over native `ParticleSystem`s.
Replaces `direct`'s `enableParticles()` globals and the Python-only `ParticleEffect`.

## Provides

- `AddParticles()` — registers the built-in physics + particle managers (`IParticles`) plus a per-frame `do_physics`/`do_particles` update task at `FrameSlots.Collision`.
- `IParticles` — `Create(name)` to build an effect, plus the native `Physics` and `Manager`.
- `ParticleEffect` — an effect root `Node` and an `IList<ParticleSystem> Systems` (adding a system attaches it to both managers); `SoftStart()`/`SoftStop()` across every system.
- `ParticleInterval` — holds a timeline slot while an effect emits, with optional soft-stop lead time and end-of-slot cleanup.

## Usage

```csharp
services.AddParticles();

var particles = provider.GetRequiredService<IParticles>();

using var effect = particles.Create("sparks");
effect.Node.ReparentTo(scene.Root);

var system = new ParticleSystem(256);
system.SetFactory(new PointParticleFactory());       // configure the native system directly
system.SetRenderer(new PointParticleRenderer());
system.SetEmitter(new SphereVolumeEmitter());
effect.Systems.Add(system);                          // attaches to the physics + particle managers
effect.SoftStart();

// or drive the effect over a timeline
var timed = new ParticleInterval(effect, scene.Root, duration: 2.0, softStopT: 0.5, cleanup: true);
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
