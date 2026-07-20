# Panda3D.Framework.Rendering

Wraps the Panda3D rendering chain (`GraphicsEngine → GraphicsPipe → GSG → GraphicsOutput → DisplayRegion → Camera → Lens`) as DI services. Owns everything per-output — windows, offscreen buffers, display regions, cameras, the 2-D overlay — and registers the `igLoop` task that drives `RenderFrame()`. The scene graph it renders belongs to `ISceneManager`; a view just points at a chosen scene root.

## Provides

- `AddEngine()` — engine/pipe/GSG singletons plus `IViewManager`, without a render task (a build can render on its own terms, or stay headless).
- `AddRendering()` — registers the `igLoop` `RenderFrame` frame task; also marks rendering as the clock tick source so `AddClock` won't double-tick.
- `AddWindow(Action<ViewOptions>?)` — the one-line path: open a single view against the default scene root at startup.
- `IViewManager` — runtime `OpenView(ViewOptions)` / `CloseView(IView)`, `Views`, `Main` / `MainOrNull`.
- `IView` — one output (window or offscreen) + its display regions + camera rig(s) + optional 2-D overlay. Exposes `Camera`/`Cameras`, `AddRegion`, `AddCamera`, `Overlay2d`/`Pixel2d`/`OverlayAnchors`, `ClearColor`, `ShowFrameRate`, and the window observables `Resized`/`Closed`/`FocusChanged`/`Minimized`.
- `ICameraRig` — composed lens/camera ergonomics: `Node`, `Lens`, `SetPerspective(fov)`, `SetOrthographic(filmW, filmH)`.
- `IRenderingService` — `Engine`, `Pipe`, `RenderFrame()`.

## Usage

```csharp
services.AddRendering();                                   // igLoop RenderFrame task (pulls in AddEngine)
services.AddWindow(o => o.Window.Title = "My Game");       // open one view at startup

// later, from gameplay (IView resolved from IViewManager):
IView view = views.Main;
view.Camera.SetPerspective(60f);
view.ClearColor = new LVecBase4f(0.53f, 0.80f, 0.92f, 1f);
using var closeSub = view.Closed.Subscribe(_ => lifetime.StopApplication());
```

Multi-window shapes are composition: `OpenView` twice against `SceneRoot = null` (shared scene) or against two named roots (independent scenes); `AddRegion` for split-screen over one window.

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
