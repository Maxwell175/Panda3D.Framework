using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Rendering;

/// <summary>
/// Engine-wide view registry. Each <see cref="OpenView"/> seeds a fresh per-output DI scope, and the
/// first-opened view becomes <see cref="Main"/> (whose close stops the application).
/// </summary>
internal sealed class ViewManager : IViewManager
{
    readonly RenderingService _rendering;
    readonly ISceneManager _scene;
    readonly IServiceScopeFactory _scopeFactory;
    readonly INamedEventBus _bus;
    readonly IHostApplicationLifetime _lifetime;
    readonly List<IView> _views = new();
    int _idCounter;

    public ViewManager(
        RenderingService rendering,
        ISceneManager scene,
        IServiceScopeFactory scopeFactory,
        INamedEventBus bus,
        IHostApplicationLifetime lifetime)
    {
        _rendering = rendering;
        _scene = scene;
        _scopeFactory = scopeFactory;
        _bus = bus;
        _lifetime = lifetime;
    }

    public IReadOnlyList<IView> Views => _views;

    IView? _main;

    public IView Main => _main ?? throw new InvalidOperationException(
        "No view has been opened. Call AddWindow(...) at startup (or OpenView(...)) before accessing Main; " +
        "use MainOrNull if a view may not exist yet.");

    public IView? MainOrNull => _main;

    public IView OpenView(ViewOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sceneRoot = options.SceneRoot is null ? _scene.Root : _scene.GetRoot(options.SceneRoot);
        int id = Interlocked.Increment(ref _idCounter);

        var scope = _scopeFactory.CreateScope();
        var view = new View(_rendering, sceneRoot, options, scope, _bus, id);
        scope.ServiceProvider.GetRequiredService<ViewContext>().View = view;

        _views.Add(view);
        if (_views.Count == 1)
        {
            _main = view;
            // closing the main window quits the app
            view.Closed.Subscribe(_ => _lifetime.StopApplication());
        }
        return view;
    }

    public void CloseView(IView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (_views.Remove(view))
            view.Dispose();
    }
}
