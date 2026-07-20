# 03 — Rendering (`Panda3D.Framework.Rendering`)

**Purpose.** Wraps the Panda3D rendering chain — `GraphicsEngine → GraphicsPipe → GSG → GraphicsOutput → DisplayRegion → Camera → Lens` — as DI services, owns everything *per-output* (windows, display regions, cameras, the 2-D overlay), and registers the `igLoop` task that drives `RenderFrame()`. The scene graph it renders belongs to `ISceneManager` ([01](01-abstractions.md)); a view simply points at a chosen scene root.

**Replaces in `direct`.** The window/pipe/camera/`DisplayRegion` setup ShowBase does in its constructor, ShowBase's `igLoop`, and ShowBase's window-event / aspect-ratio handling (`windowEvent`/`adjustWindowAspectRatio`).

**Dependencies.** `Abstractions`, `Events` (the window observables ride the pump), `Scheduling` (the `igLoop` task and render tick-source marker), the fork's C# bindings. (Input depends on *this* for the window, not the reverse.)

**Public surface.**
```csharp
// One view = one window + its display region(s) + camera rig + 2-D overlay, pointed at a scene root.
public interface IView : IDisposable {
    IGraphicsOutput Output { get; }             // the binding output, used directly (window or offscreen buffer)
    GraphicsWindow? Window { get; }             // null for offscreen views
    IServiceProvider Services { get; }          // this view's DI scope (per-view services: IInput, IGui, …)
    bool IsClosed { get; }
    ICameraRig Camera { get; }                  // the main 3-D camera rig
    IReadOnlyList<ICameraRig> Cameras { get; }  // the main rig + any added via AddCamera
    ICameraRig AddCamera(NodePath? scene = null); // extra rig (split-screen/PiP/minimap); scene defaults to this view's root
    LVecBase4f ClearColor { get; set; }         // the output's background clear color; setting it also enables clearing
    NodePath? Overlay2d { get; }                // aspect2d-equivalent root (kept aspect-correct; null when Setup2d=false)
    NodePath? Pixel2d { get; }                  // pixel-exact root (1 unit = 1 pixel; null when Setup2d=false)
    IReadOnlyDictionary<OverlayAnchor, NodePath> OverlayAnchors { get; } // keyed by the OverlayAnchor enum: TopLeft, BottomRight, Center, …
    IReadOnlyList<IDisplayRegion> Regions { get; }
    IDisplayRegion AddRegion(DisplayRegionOptions o);   // split-screen / extra cameras
    void ShowFrameRate();  void HideFrameRate();        // built-in frame-rate meter in this view's corner (idempotent)

    // Window notifications, as System.Reactive observables (see 06). All demuxed from Panda's single
    // per-window "window-event" by diffing WindowProperties — see "Window-event demux" below.
    IObservable<WindowSize> Resized { get; }    // size changed
    IObservable<Unit>       Closed { get; }     // properties.get_open() went false
    IObservable<bool>       FocusChanged { get; }  // get_foreground() transitioned (true = foreground)
    IObservable<bool>       Minimized { get; }     // get_minimized() transitioned (true = minimized)
}
public readonly record struct WindowSize(int Width, int Height);
public interface IViewManager {                 // engine-wide; runtime open/close
    IView OpenView(ViewOptions o);              // creates + seeds a per-output scope (see note)
    void CloseView(IView view);                 // disposes that scope deterministically
    IReadOnlyList<IView> Views { get; }
    IView Main { get; }                         // the default view (first-opened); throws if none open yet
    IView? MainOrNull { get; }                  // the default view, or null if none open yet
}

// The one rendering wrapper that earns its place: composed lens/camera ergonomics (not passthrough).
public interface ICameraRig { NodePath Node { get; } Lens Lens { get; } void SetPerspective(float fov); void SetOrthographic(float filmW, float filmH); }
public interface IRenderingService { GraphicsEngine Engine { get; } GraphicsPipe Pipe { get; } void RenderFrame(); }

public sealed class ViewOptions {
    public string? SceneRoot { get; set; }      // null = ISceneManager.Root; else a named root (multi-root)
    public WindowOptions Window { get; set; } = new();
    public bool Setup2d { get; set; } = true;   // create the aspect2d/pixel2d overlay (on by default)
    public bool Offscreen { get; set; }         // create an offscreen buffer instead of a window
}

public static class RenderingServiceCollectionExtensions {
    public static IServiceCollection AddEngine(this IServiceCollection s);                 // engine, pipe, GSG, IViewManager — singletons (no render task)
    public static IServiceCollection AddRendering(this IServiceCollection s);              // registers the igLoop render task (FrameSlots.Render)
    public static IServiceCollection AddWindow(this IServiceCollection s, Action<ViewOptions>? o = null); // simple default: open one view at startup
}
```

**Engine-wide vs per-view.** `AddEngine` registers the singletons from `GraphicsEngine.GetGlobalPtr()` and `GraphicsPipeSelection.GetGlobalPtr().MakeDefaultPipe()` plus the `IViewManager` — but **not** the render task, so it can exist without driving a standard frame render. `AddRendering` registers the `igLoop` task; a headless build simply omits it (00 §6). `AddWindow` is the **one-line prototype path**: it opens a single `IView` against `ISceneManager.Root` at startup (2-D overlay on). Everything beyond that goes through `IViewManager`. (The usual client registers all three; headless registers neither rendering nor window.)

**Views, roots, and runtime windows.** Scene roots (`ISceneManager`, [01](01-abstractions.md)) and windows are independent axes; an `IView` binds one window to one chosen root, so every multi-window shape is composition, not a special case:
- **Two windows, one shared scene:** `OpenView` twice with `SceneRoot = null` (both show `ISceneManager.Root`); give each its own camera rig (or share).
- **Two windows, two independent roots (the DS-two-screens shape):** `scene.GetRoot("top")` / `scene.GetRoot("bottom")`, then `OpenView` once against each.
- **Split-screen, one window:** one view, `AddRegion(...)` for extra display regions/cameras over the same root.
- **Runtime open/close:** `OpenView` mid-game creates a fresh per-output scope (window, regions, camera rig, 2-D overlay, **and the per-window input chain** — [05](05-input.md) — plus its window-close watcher); `CloseView` disposes that scope, deterministically tearing all of it down (the `IDisposable` model from [01](01-abstractions.md)).

`IViewManager` is imperative and works on bare MS.DI. A developer who wants views as **resolvable named services** can layer that on with a container that supports runtime registration (e.g. DryIoc) and register each `IView` under a key — the framework neither requires nor prevents it (container-agnostic, 00 §4).

**`igLoop` registration.**
```csharp
services.AddFrameTask("igLoop", FrameSlots.Render, sp =>
{
    var rendering = sp.GetRequiredService<IRenderingService>();
    return () => { rendering.RenderFrame(); return true; };  // renders all views' outputs; ticks the global clock in rendered builds
});
```
One `igLoop` renders every open view's output.

**Why the 2-D overlay lives on the view (not on `ISceneManager`).** Under the hood `render` and `render2d` are the *same kind of object* — both `NodePath` roots; the only real difference is camera/lens (perspective vs orthographic). But they *feel* different: a 3-D world is shared and exists independently of any screen, while a 2-D overlay is intrinsically *about* a screen (sized to the window, scaled to its aspect, anchored to its corners). So the surfaces are split to match that intuition — 3-D world roots on `ISceneManager` ([01](01-abstractions.md)), the 2-D overlay here on `IView`, created and aspect-managed per view. `ISceneManager` does not vend 2-D roots.

**2-D overlay and resize (faithful to `direct`, on by default).** Each view sets up an `aspect2d`-equivalent overlay: a display region layered above the 3-D one (a *display-region* sort, e.g. 10 — distinct from the `FrameSlots` task sorts) with an `OrthographicLens`, clear-color off / clear-depth on, plus a `pixel2d` root. Both roots are **`PGTop` nodes wired to the view's `MouseWatcher`** (`set_mouse_watcher`), exactly as ShowBase builds `aspect2d`/`pixel2d` — required for PGui widgets ([10](10-gui.md)) to receive input. On each `Resized`, the view reproduces ShowBase's `adjustWindowAspectRatio`:
- `cameraLens.SetAspectRatio(ar)`;
- if **wide** (`ar ≥ 1`): `overlay2d.SetScale(1/ar, 1, 1)`, edges at left/right = ∓ar, top/bottom = ±1;
- if **tall** (`ar < 1`): `overlay2d.SetScale(1, ar, ar)`, edges at top/bottom = ±1/ar, left/right = ∓1;
- `pixel2d.SetScale(2/width, 1, 2/height)` so one unit = one pixel.

The view also maintains the `OverlayAnchors` (keyed by the `OverlayAnchor` enum — `TopLeft`, `BottomRight`, `Center`, …) under `Overlay2d` so corner-anchored UI sticks to corners as the output resizes, exactly as ShowBase does. `Setup2d = false` skips all of this for views that don't need an overlay (e.g. an offscreen or pure-3-D secondary output).

**Multi-window resource sharing.** When a view should share textures/vertex buffers/RTT with another (rather than be fully independent), open its output sharing the first view's GSG: under the wrapper, `engine.MakeOutput(pipe, name, 0, fb, wp, BF_require_window, gsgA, hostA)` with `gsgA = firstWindow.Window.GetGsg()`. Keep all shared-resource windows on the default pipe. Fully independent windows (different roots, no shared resources) just get their own GSG.

**Design notes.**
- **How per-view services bind to their view (the seeded scope).** `OpenView` creates the scope from `IServiceScopeFactory` and immediately seeds a scoped holder — `ViewContext { IView? View }` — the `IHttpContextAccessor` idiom, view-shaped. Per-view services (`IGui`, `IInput`, …) take `ViewContext` in their constructor, so resolving them **from `view.Services`** binds them to that view — fully explicit, no ambient state: `viewB.Services.GetRequiredService<IGui>()`. Resolving a per-view service from the **root** provider has no view to bind to and throws, so per-view services are always reached through a view: get the main view via `IViewManager.Main` (or `MainOrNull` when one may not exist yet) and resolve from its `Services` — [Input](05-input.md) adds a `view.Input()` shortcut. Closing a view disposes its scope, which disposes every service and subscription created in it.
- **GSG is the one cross-tier object.** Each output has one GSG, but a shared GSG can back several outputs; model the shared GSG as a singleton injected into per-view scopes when sharing is wanted.
- **Render is movable.** Because it's a sorted task, half-rate render, an RTT pre-pass (lower sort), or additional views are registration/runtime changes, not loop changes.
- **Clock ownership.** `RenderFrame()` advances the global clock in rendered builds. Rendering registers an `IClockTickSource` marker so [07](07-scheduling-and-time.md)'s `AddClock` does not also enable `tick_clock` on the default chain. Dropping this project for a headless build does not stop the clock: with no render tick source registered, `AddClock` enables chain ticking instead.
- **Owns window notifications + shutdown wiring.** Panda raises a single `"window-event"` per `GraphicsWindow`, parameterized only by the window — there is **no** resize/close/focus sub-type in the event. The view subscribes to its window's event name (a unique `set_window_event($"window-event-{id}")` per view) through the [Events](06-events.md) pump and **demuxes** one stream into four typed observables by diffing `WindowProperties` against the previously-seen snapshot (exactly as `ShowBase.windowEvent` does): `get_open()` false → `Closed`; `get_foreground()` transition → `FocusChanged`; `get_minimized()` transition → `Minimized`; `has_size()`/size change → `Resized` (which also drives the aspect-ratio recompute below). Diffing is essential — a window-event fires for *any* property change, so naively pushing `Resized` on every event would be wrong. The view that owns the **main** window also subscribes its own `Closed` to call `life.StopApplication()` (the close-to-quit path promised in [02](02-hosting.md) §4); closing a secondary view just disposes that view.
- **Window-event demux is the general pattern.** This "one engine event → several typed observables, separated by inspecting payload/state" shape recurs (device events, collision events): a project subscribes to a Panda event name once and routes to the right `Subject<T>` after examining the event. Documented here because window-event is the clearest case.

**Open items.**
- Confirm opening a second `GraphicsWindow` at runtime on the same engine/pipe behaves on each target platform (the published API supports it; the samples never exercise it).

> **Verified:** `MultiViewTests` cover runtime open/close, two offscreen outputs over one shared scene, two offscreen outputs over independent roots, and split-screen display regions. `RenderingIntegrationTests` cover offscreen rendering, clock advancement through `RenderFrame`, 2-D overlay roots, pixel roots, and named overlay anchors.

> **Verified (1.11 headers):** `make_output(pipe, name, sort, fb, win, flags, gsg = nullptr, host = nullptr)` is a single `PUBLISHED` method — the GSG-sharing `gsg`/`host` arguments are optional trailing params, so the resource-sharing recipe is real. `GraphicsOutput::get_gsg()` and the `gsg` property are `PUBLISHED`, as are `set_threading_model`, `set_auto_flip`, `default_loader`, and the `make_buffer(gsg|host, …)` shorthands. ShowBase's resize behavior above is from `direct/src/showbase/ShowBase.py` (`windowEvent`, `adjustWindowAspectRatio`, `getAspectRatio`).

**See also.** [02 Hosting](02-hosting.md) (`FrameSlots.Render`, window-close task); [01 Abstractions](01-abstractions.md) (`ISceneManager` roots a view points at); [05 Input](05-input.md) (per-view input chain); [06 Events](06-events.md) (window-event bridge); [10 GUI](10-gui.md) (parents `PGTop` into `Overlay2d`/`Pixel2d`).
