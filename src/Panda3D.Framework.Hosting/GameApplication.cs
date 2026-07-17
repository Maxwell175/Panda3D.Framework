using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Hosting;

/// <summary>
/// The composed game application. The frame is supplied entirely by native sorted tasks on the
/// <c>"default"</c> chain (see <see cref="FrameSlots"/>); the core loop does only <c>Poll()</c>.
/// </summary>
public sealed class GameApplication
{
    readonly IHost _host;

    internal GameApplication(IHost host) => _host = host;

    /// <summary>Start composing a game application (ASP.NET-style builder).</summary>
    public static IGameApplicationBuilder CreateBuilder(string[] args) => new GameApplicationBuilder(args);

    /// <summary>The root service provider.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Start the hosted services (which materialize all native sorted tasks), spawn the bootstrap
    /// coroutine, then pump <c>Poll()</c> on the calling thread until shutdown, and stop cleanly.
    /// </summary>
    public void Run()
    {
        // blocking on purpose: awaiting before the pump installs the sync context would resume
        // off-thread and break window / GL-context affinity
        _host.StartAsync().GetAwaiter().GetResult();

        var sp = _host.Services;
        var life = sp.GetRequiredService<IHostApplicationLifetime>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Panda3D.Framework.Hosting");
        var tasks = AsyncTaskManager.GetGlobalPtr();

        // surface task faults instead of losing them silently: coroutine faults and frame-task faults
        void OnUnobserved(Exception ex) =>
            logger?.LogError(ex, "Unobserved exception on a Panda coroutine.");
        void OnFrameTaskError(Exception ex) =>
            logger?.LogError(ex, "Unhandled exception in a frame task.");
        PandaTaskScheduler.UnobservedException += OnUnobserved;
        FrameTaskDiagnostics.UnhandledException += OnFrameTaskError;

        try
        {
            // entry coroutine resumes in the gameplay slot
            var bootstrap = sp.GetService<IBootstrap>();
            if (bootstrap is not null)
                PandaTask.Spawn(() => bootstrap.RunAsync());

            var stopping = life.ApplicationStopping;
            while (!stopping.IsCancellationRequested)
                tasks.Poll();   // the entire core loop
        }
        finally
        {
            PandaTaskScheduler.UnobservedException -= OnUnobserved;
            FrameTaskDiagnostics.UnhandledException -= OnFrameTaskError;
            _host.StopAsync().GetAwaiter().GetResult();   // hosted services remove their native tasks
            _host.Dispose();
        }
    }
}
