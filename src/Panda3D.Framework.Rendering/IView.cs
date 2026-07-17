using System;
using System.Collections.Generic;
using System.Reactive;
using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>
/// One view = one output (window or offscreen buffer) + its display region(s) + camera rig + optional
/// 2-D overlay, pointed at a scene root. Disposing a view tears down its whole per-output scope.
/// </summary>
public interface IView : IDisposable
{
    /// <summary>The output backing this view (a window or an offscreen buffer), used directly.</summary>
    IGraphicsOutput Output { get; }

    /// <summary>The window, when this view is windowed; <see langword="null"/> for an offscreen view.</summary>
    GraphicsWindow? Window { get; }

    /// <summary>This view's DI scope (per-view services: IInput, IGui, …).</summary>
    IServiceProvider Services { get; }

    /// <summary>Whether the output has been closed/torn down.</summary>
    bool IsClosed { get; }

    /// <summary>The main 3-D camera rig.</summary>
    ICameraRig Camera { get; }

    /// <summary>All camera rigs on this view — the main one plus any added via <see cref="AddCamera"/>.</summary>
    IReadOnlyList<ICameraRig> Cameras { get; }

    /// <summary>The output's background clear color. Setting it also enables clearing.</summary>
    LVecBase4f ClearColor { get; set; }

    /// <summary>The <c>aspect2d</c>-equivalent overlay root (aspect-correct). Null when <c>Setup2d</c> is off.</summary>
    NodePath? Overlay2d { get; }

    /// <summary>The pixel-exact 2-D root (1 unit = 1 pixel). Null when <c>Setup2d</c> is off.</summary>
    NodePath? Pixel2d { get; }

    /// <summary>
    /// Children under <see cref="Overlay2d"/> kept positioned at the current aspect-correct edges and
    /// corners — parent a widget to <c>OverlayAnchors[OverlayAnchor.TopLeft]</c> to pin it there.
    /// Empty when <c>Setup2d</c> is off.
    /// </summary>
    IReadOnlyDictionary<OverlayAnchor, NodePath> OverlayAnchors { get; }

    /// <summary>The view's display regions.</summary>
    IReadOnlyList<IDisplayRegion> Regions { get; }

    /// <summary>Add a display region (split-screen / extra cameras) over this output.</summary>
    IDisplayRegion AddRegion(DisplayRegionOptions options);

    /// <summary>
    /// Add a second 3-D camera rig (for split-screen, picture-in-picture, or a minimap). Bind it to a
    /// region via <see cref="DisplayRegionOptions.Camera"/>. <paramref name="scene"/> defaults to this
    /// view's scene root; pass another root to look at a different scene.
    /// </summary>
    ICameraRig AddCamera(NodePath? scene = null);

    /// <summary>
    /// Show the built-in frame-rate meter in the corner of this view. Idempotent; hidden with
    /// <see cref="HideFrameRate"/> and torn down with the view.
    /// </summary>
    void ShowFrameRate();

    /// <summary>Hide the frame-rate meter shown by <see cref="ShowFrameRate"/>.</summary>
    void HideFrameRate();

    /// <summary>Window resized.</summary>
    IObservable<WindowSize> Resized { get; }

    /// <summary>Window closed.</summary>
    IObservable<Unit> Closed { get; }

    /// <summary>Foreground/focus transitioned (true = foreground).</summary>
    IObservable<bool> FocusChanged { get; }

    /// <summary>Minimized transitioned (true = minimized).</summary>
    IObservable<bool> Minimized { get; }
}
