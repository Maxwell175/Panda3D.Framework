using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Rendering;

/// <summary>
/// Registration for the engine, views, the render task, and the one-line window path.
/// </summary>
public static class RenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine/pipe singletons and <see cref="IViewManager"/>, but not the render task.
    /// Requires <c>AddSceneManager</c> and <c>AddEvents</c>.
    /// </summary>
    public static IServiceCollection AddEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<RenderingService>();
        services.TryAddSingleton<IRenderingService>(sp => sp.GetRequiredService<RenderingService>());
        services.TryAddSingleton<IViewManager, ViewManager>();
        services.TryAddScoped<ViewContext>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, FrameCompositionValidator>());
        return services;
    }

    /// <summary>
    /// Registers the <c>igLoop</c> render task at <see cref="FrameSlots.Render"/>. Also marks that
    /// <c>RenderFrame</c> ticks the clock, so <c>AddClock</c> won't double-tick.
    /// </summary>
    public static IServiceCollection AddRendering(this IServiceCollection services)
    {
        services.AddEngine();
        services.TryAddSingleton<IClockTickSource, RenderClockTickSource>();
        services.AddFrameTask("igLoop", FrameSlots.Render, sp =>
        {
            var rendering = sp.GetRequiredService<IRenderingService>();
            return () => { rendering.RenderFrame(); return true; };
        });
        return services;
    }

    /// <summary>Open a single view against the default scene root at startup.</summary>
    public static IServiceCollection AddWindow(this IServiceCollection services, Action<ViewOptions>? configure = null)
    {
        services.AddEngine();
        services.AddSingleton<IHostedService>(sp =>
            new InitialViewService(sp.GetRequiredService<IViewManager>(), configure));
        return services;
    }
}

/// <summary>Opens the startup view on host start (on the main thread, for window/GL affinity).</summary>
internal sealed class InitialViewService : IHostedService
{
    readonly IViewManager _views;
    readonly Action<ViewOptions>? _configure;
    IView? _view;

    public InitialViewService(IViewManager views, Action<ViewOptions>? configure)
    {
        _views = views;
        _configure = configure;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = new ViewOptions();
        _configure?.Invoke(options);
        _view = _views.OpenView(options);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_view is not null) _views.CloseView(_view);
        _view = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// After host start, warns once if a window is open but nothing drives the frame — the "black window"
/// of calling <c>AddWindow</c> without <c>AddRendering</c>. Self-clears if any render loop or clock
/// tick source exists.
/// </summary>
internal sealed class FrameCompositionValidator : IHostedService
{
    readonly IServiceProvider _provider;
    readonly IViewManager _views;
    readonly IHostApplicationLifetime _lifetime;
    readonly ILogger<FrameCompositionValidator> _logger;

    public FrameCompositionValidator(
        IServiceProvider provider, IViewManager views,
        IHostApplicationLifetime lifetime, ILogger<FrameCompositionValidator> logger)
    {
        _provider = provider;
        _views = views;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // run after all hosted services start, so registration order doesn't matter
        _lifetime.ApplicationStarted.Register(Validate);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    void Validate()
    {
        bool windowOpen = _views.Views.Any(v => v.Window is not null);
        bool driven = _provider.GetService<IClockTickSource>() is not null;
        if (windowOpen && !driven)
            _logger.LogWarning(
                "A window is open but no render loop or clock tick source is registered — nothing will " +
                "draw and the clock won't advance unless you drive it yourself. Did you mean to call AddRendering()?");
    }
}
