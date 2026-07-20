# Panda3D.Framework.Hosting

Owns the application lifecycle and the frame loop. Provides the ASP.NET-style `GameApplication` (`builder → Build → Run`) whose core pump does only `taskManager.Poll()`; rendering, input, and gameplay compose into the frame as native sorted tasks (ordered by `FrameSlots`). Replaces `direct`'s `ShowBase.run()` — the app is composed, not subclassed.

## Provides

- `GameApplication` — `CreateBuilder(args)`, `Services`, `Run()` (pumps `Poll()` on the calling thread until shutdown).
- `IGameApplicationBuilder` — `Services`, `Configuration`, `Build()`.
- `IBootstrap` — the app's entry coroutine (`RunAsync`), spawned at the gameplay slot right after the host starts.
- `AddBootstrap<T>()` / `AddBootstrap(Func<IServiceProvider, PandaTask>)` / `AddBootstrap(Delegate)` — register the entry coroutine (typed, delegate, or minimal-API-style injected parameters).
- `AddSceneManager()` — registers `ISceneManager`, the world-root service (a `render` root + named independent roots).

```csharp
var builder = GameApplication.CreateBuilder(args);
builder.Services.AddSceneManager();
builder.Services.AddClock();       // Panda3D.Framework.Scheduling
builder.Services.AddScheduler();   // Panda3D.Framework.Scheduling
builder.Services.AddBootstrap(async (IGameClock clock, IHostApplicationLifetime life) =>
{
    for (int i = 0; i < 60; i++) await PandaTask.NextFrame();
    life.StopApplication();
});
builder.Build().Run();
```

Depends on `Microsoft.Extensions.Hosting` (configuration, logging, options, `IHostApplicationLifetime`, `IHostedService`) and on `Panda3D.Framework.Scheduling`. Setup-time native sorted tasks are registered with `AddFrameTask(name, sort, …)` and the gameplay-facing `IFrameScheduler` — both from the Scheduling package.

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
