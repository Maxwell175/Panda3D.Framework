using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using Interrogate;
using Microsoft.Extensions.DependencyInjection;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Rendering;

/// <summary>
/// One output's worth of rendering state: the window/buffer, its display region(s), the 3-D camera
/// rig, the optional 2-D overlay, the window-event demux, and the per-view DI scope.
/// </summary>
internal sealed class View : IView
{
    // sorts above the full-output 3-D region (sort 0) so the HUD draws over the scene
    const int OverlayRegionSort = 10;

    readonly GraphicsEngine _engine;
    readonly IServiceScope _scope;
    readonly NodePath _sceneRoot;
    readonly int _id;
    readonly List<IDisplayRegion> _regions = new();
    readonly List<ICameraRig> _cameras = new();
    readonly Dictionary<OverlayAnchor, NodePath> _overlayAnchors = new();

    readonly Subject<WindowSize> _resized = new();
    readonly Subject<Unit> _closed = new();
    readonly Subject<bool> _focus = new();
    readonly Subject<bool> _minimized = new();

    IDisposable? _windowEventSub;
    FrameRateMeter? _frameRateMeter;
    WindowSnapshot _prevSnapshot;
    int _disposed;

    public View(RenderingService rendering, NodePath sceneRoot, ViewOptions options,
                IServiceScope scope, INamedEventBus bus, int id)
    {
        _engine = rendering.Engine;
        _scope = scope;
        _sceneRoot = sceneRoot;
        _id = id;

        Output = CreateOutput(rendering, options, id);
        Window = options.Offscreen ? null : Output.CastTo<GraphicsWindow>();

        Camera = AddCamera();
        var region = Output.MakeDisplayRegion();
        region.SetCamera(Camera.Node);
        _regions.Add(region);

        if (options.Setup2d)
            SetupOverlay(id);

        ApplyAspect(Output.GetXSize(), Output.GetYSize());

        // windowed views only; buffers raise no window events
        if (Window is not null)
        {
            string eventName = $"window-event-{id}";
            Window.SetWindowEvent(eventName);
            _prevSnapshot = Snapshot(Window);
            _windowEventSub = bus.Observe(eventName).Subscribe(_ => OnWindowEvent());
        }
    }

    public IGraphicsOutput Output { get; }
    public GraphicsWindow? Window { get; }
    public IServiceProvider Services => _scope.ServiceProvider;
    public ICameraRig Camera { get; }
    public IReadOnlyList<ICameraRig> Cameras => _cameras;

    public LVecBase4f ClearColor
    {
        get => Output.GetClearColor();
        set
        {
            Output.SetClearColorActive(true);
            Output.SetClearColor(value);
        }
    }
    public NodePath? Overlay2d { get; private set; }
    public NodePath? Pixel2d { get; private set; }
    public IReadOnlyDictionary<OverlayAnchor, NodePath> OverlayAnchors => _overlayAnchors;
    public IReadOnlyList<IDisplayRegion> Regions => _regions;

    public bool IsClosed => _disposed != 0 || (Window?.IsClosed() ?? false);

    public IObservable<WindowSize> Resized => _resized;
    public IObservable<Unit> Closed => _closed;
    public IObservable<bool> FocusChanged => _focus;
    public IObservable<bool> Minimized => _minimized;

    public IDisplayRegion AddRegion(DisplayRegionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (l, r, b, t) = options.Dimensions;
        var region = Output.MakeDisplayRegion(l, r, b, t);
        region.SetSort(options.Sort);
        if (options.Camera is not null) region.SetCamera(options.Camera);
        _regions.Add(region);
        return region;
    }

    public ICameraRig AddCamera(NodePath? scene = null)
    {
        var target = scene ?? _sceneRoot;
        var rig = new CameraRig(target, target, $"camera-{_id}-{_cameras.Count}", new PerspectiveLens());
        _cameras.Add(rig);
        return rig;
    }

    public void ShowFrameRate()
    {
        _frameRateMeter ??= new FrameRateMeter($"frame-rate-meter-{_id}");
        _frameRateMeter.SetupWindow(Output);
    }

    public void HideFrameRate()
    {
        _frameRateMeter?.ClearWindow();
        _frameRateMeter = null;
    }

    static IGraphicsOutput CreateOutput(RenderingService rendering, ViewOptions options, int id)
    {
        var fb = new FrameBufferProperties();
        fb.SetRgbColor(true);
        fb.SetColorBits(24);
        fb.SetDepthBits(24);
        fb.SetBackBuffers(options.Offscreen ? 0 : 1);

        var wp = WindowProperties.GetDefault();
        wp.SetSize(options.Window.Size.W, options.Window.Size.H);
        wp.SetTitle(options.Window.Title);

        int flags = (int)(options.Offscreen
            ? GraphicsPipeBufferCreationFlags.BfRefuseWindow
            : GraphicsPipeBufferCreationFlags.BfRequireWindow);

        return rendering.Engine.MakeOutput(rendering.Pipe, $"view-{id}", 0, fb, wp, flags)
            ?? throw new InvalidOperationException(
                $"MakeOutput returned null (offscreen={options.Offscreen}); no usable graphics output could be created.");
    }

    void SetupOverlay(int id)
    {
        var render2d = new NodePath($"render2d-{id}");
        render2d.SetDepthTest(false);
        render2d.SetDepthWrite(false);
        render2d.SetBin("unsorted", 0);

        var lens2d = new OrthographicLens();
        lens2d.SetFilmSize(2f, 2f);
        lens2d.SetNearFar(-1000f, 1000f);
        var cam2d = new Camera($"camera2d-{id}", lens2d);
        cam2d.SetScene(render2d);
        var cam2dNp = render2d.AttachNewNode(cam2d);

        var dr2d = Output.MakeDisplayRegion();
        dr2d.SetSort(OverlayRegionSort);
        dr2d.SetCamera(cam2dNp);
        dr2d.SetClearColorActive(false);
        dr2d.SetClearDepthActive(true);
        _regions.Add(dr2d);

        // Input wires these PGTop roots to the view's MouseWatcher when present
        Overlay2d = render2d.AttachNewNode(new PGTop($"aspect2d-{id}"));
        Pixel2d = render2d.AttachNewNode(new PGTop($"pixel2d-{id}"));
        CreateOverlayAnchors(id);
    }

    void CreateOverlayAnchors(int id)
    {
        if (Overlay2d is null) return;

        foreach (OverlayAnchor anchor in Enum.GetValues<OverlayAnchor>())
            _overlayAnchors[anchor] = Overlay2d.AttachNewNode($"aspect2d-{id}-{anchor}");
    }

    void ApplyAspect(int width, int height)
    {
        float ar = Overlay2dMath.AspectRatio(width, height);
        Camera.Lens.SetAspectRatio(ar);

        if (Overlay2d is not null)
        {
            var s = Overlay2dMath.OverlayScale(ar);
            Overlay2d.SetScale(new LVecBase3f(s.X, s.Y, s.Z));
            PositionOverlayAnchors(ar);
        }
        if (Pixel2d is not null)
        {
            var p = Overlay2dMath.PixelScale(width, height);
            Pixel2d.SetScale(new LVecBase3f(p.X, p.Y, p.Z));
        }
    }

    void PositionOverlayAnchors(float aspectRatio)
    {
        if (_overlayAnchors.Count == 0) return;

        var e = Overlay2dMath.OverlayEdges(aspectRatio);
        SetAnchor(OverlayAnchor.Center, 0f, 0f);
        SetAnchor(OverlayAnchor.Top, 0f, e.Top);
        SetAnchor(OverlayAnchor.Bottom, 0f, e.Bottom);
        SetAnchor(OverlayAnchor.Left, e.Left, 0f);
        SetAnchor(OverlayAnchor.Right, e.Right, 0f);
        SetAnchor(OverlayAnchor.TopLeft, e.Left, e.Top);
        SetAnchor(OverlayAnchor.TopRight, e.Right, e.Top);
        SetAnchor(OverlayAnchor.BottomLeft, e.Left, e.Bottom);
        SetAnchor(OverlayAnchor.BottomRight, e.Right, e.Bottom);
    }

    void SetAnchor(OverlayAnchor key, float x, float z)
    {
        if (_overlayAnchors.TryGetValue(key, out var anchor))
            anchor.SetPos(x, 0f, z);
    }

    void OnWindowEvent()
    {
        if (_disposed != 0 || Window is null) return;

        var curr = Snapshot(Window);
        var changes = WindowEventDemux.Diff(_prevSnapshot, curr);
        _prevSnapshot = curr;

        if (changes.Resized)
        {
            ApplyAspect(curr.Width, curr.Height);
            _resized.OnNext(new WindowSize(curr.Width, curr.Height));
        }
        if (changes.FocusChanged) _focus.OnNext(changes.Foreground);
        if (changes.MinimizedChanged) _minimized.OnNext(changes.Minimized);
        if (changes.Closed) _closed.OnNext(Unit.Default);
    }

    static WindowSnapshot Snapshot(GraphicsWindow window)
    {
        var p = window.GetProperties();
        bool hasSize = p.HasSize();
        return new WindowSnapshot(
            p.GetOpen(), p.GetForeground(), p.GetMinimized(),
            hasSize, hasSize ? p.GetXSize() : 0, hasSize ? p.GetYSize() : 0);
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _frameRateMeter?.ClearWindow();
        _windowEventSub?.Dispose();
        _resized.OnCompleted();
        _closed.OnCompleted();
        _focus.OnCompleted();
        _minimized.OnCompleted();

        _scope.Dispose();
        try { _engine.RemoveWindow(Output); } catch { /* already gone */ }
    }
}
