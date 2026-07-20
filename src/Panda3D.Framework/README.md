# Panda3D.Framework

The complete framework in one package. A single `PackageReference` brings **every feature library, the C# bindings, and the native runtimes** — everything needed to build *and* run a Panda3D game in C#. The framework replaces Python's `direct` (ShowBase, taskMgr, messenger, intervals, Actor, GUI, …) with C# building blocks composed by dependency injection, on an ASP.NET-style `builder → Build → Run` host.

## Getting started

```csharp
using System;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Input;
using Panda3D.Framework.Rendering;

var builder = GameApplication.CreateBuilder(args);

builder.Services.AddGame(o => o.Window.Title = "Spinning Card");

builder.Services.AddViewBootstrap(async (
    ISceneManager scene, IViewManager views, IInput input,
    IGameClock clock, IHostApplicationLifetime life) =>
{
    var view = views.Main;
    view.ClearColor = new LVecBase4f(0.1f, 0.12f, 0.18f, 1f);

    var maker = new CardMaker("card");
    maker.SetFrame(-1f, 1f, -1f, 1f);
    maker.SetColor(0.95f, 0.45f, 0.2f, 1f);
    var card = scene.Root.AttachNewNode(maker.Generate());
    card.SetPos(0f, 12f, 0f);

    using var onClose = view.Closed.Subscribe(_ => life.StopApplication());

    while (!life.ApplicationStopping.IsCancellationRequested && !input.Pressed(Keys.Escape))
    {
        float dt = (float)clock.Dt;
        card.SetH(card.GetH() + 60f * dt);
        float x = (input.IsDown(Keys.Right) ? 1f : 0f) - (input.IsDown(Keys.Left) ? 1f : 0f);
        float z = (input.IsDown(Keys.Up) ? 1f : 0f) - (input.IsDown(Keys.Down) ? 1f : 0f);
        card.SetPos(card.GetX() + x * 8f * dt, card.GetY(), card.GetZ() + z * 8f * dt);
        await PandaTask.NextFrame();
    }
    life.StopApplication();
});

builder.Build().Run();
```

`GameApplication.CreateBuilder` gives you configuration, logging, options, and `IHostApplicationLifetime` for free. `AddGame` wires the standard windowed-game services. `AddViewBootstrap` registers your entry coroutine **in the main view's scope**, so per-view services (`IInput`, `IGui`) inject straight into it alongside root singletons like `ISceneManager` and `IGameClock`. `Run()` pumps frames on the main thread until shutdown.

## What's in the box

`AddGame()` registers the standard windowed set — **scene, events, clock, scheduler, rendering, a window, and input**. Layer the rest on top: `AddActors()`, `AddCollision()`, `AddIntervals()`, `AddGui()`, `AddAudio()`, `AddParticles()`, …

Bundled feature libraries:

- **Rendering** — engine/pipe/window/camera and the render loop; `IView`, `IViewManager`, `ICameraRig`.
- **Input** — data-graph traversal, pull-based input + action map; `IInput`, `IInputContext`, `IDevices`.
- **Physics** — collision traversal with observables over native entries; `ICollisionWorld`.
- **Actors** — skeletal animation over native `AnimControl`/`PartBundle`; `IActor`, `ActorInterval`, cross-fades.
- **Intervals** — Lerp/Sequence/Parallel/Func timelines over Panda's C++ interval system; awaitable, scrubbable.
- **Audio** — 2-D and 3-D audio over native managers/sounds; `IAudio`, `IAudio3D`.
- **Particles** — particle systems; `IParticles`, `ParticleEffect`, `ParticleInterval`.
- **Gui** — explicit `Widget` classes (`Button`/`Entry`/`Slider`/…) over PGui.
- **Events** — a queue-drain pump replacing the messenger; object notifications as `IObservable<T>`, plus `INamedEventBus` for dynamic string events.
- **Scheduling** — frame scheduler + game clock; `IFrameScheduler`, `IScheduledTask`, `IGameClock`.
- **Hosting** — the `Poll()`-only main loop; `GameApplication`, `AddBootstrap`, `AddSceneManager`.
- **Abstractions** — the `IXxx` contracts, `XxxOptions`, and `AddXxx` signatures the libraries implement.

The C# bindings (`Maxwell175/panda3d` + `Panda3d.Async`) and the four native runtimes (`linux-x64`, `osx-x64`, `osx-arm64`, `win-x64`) come along automatically; NuGet deploys only your build's target RID.

For the build-time asset pipeline (egg→bam, multifile packing), add [`Panda3D.Framework.Build`](https://www.nuget.org/packages/Panda3D.Framework.Build).

## License

BSD-3-Clause.
