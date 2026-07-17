using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Panda3D.Core;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// Registers per-frame native tasks bracketed to the host lifetime: one shared host starts them all on
/// <c>StartAsync</c> and disposes them on <c>StopAsync</c>.
/// </summary>
public static class FrameTaskRegistration
{
    /// <summary>
    /// Register a per-frame native task named <paramref name="name"/> at <paramref name="sort"/>.
    /// <paramref name="tick"/> is a factory invoked once at host start with the root
    /// <see cref="IServiceProvider"/> that returns the per-epoch callback (<see langword="true"/> to
    /// continue, <see langword="false"/> to remove). Idempotent by <paramref name="name"/>: registering
    /// the same name twice is a no-op.
    /// </summary>
    public static IServiceCollection AddFrameTask(
        this IServiceCollection services, string name, int sort,
        Func<IServiceProvider, Func<bool>> tick)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(tick);

        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(FrameTaskSpec) &&
                descriptor.ImplementationInstance is FrameTaskSpec existing &&
                existing.Name == name)
                return services;

        services.AddSingleton(new FrameTaskSpec(name, sort, tick));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FrameTaskHost>());
        return services;
    }

    /// <summary>
    /// Convenience overload: <paramref name="run"/> is handed a fresh <see cref="FrameContext"/> each epoch
    /// and returns <see cref="TaskResult.Continue"/> or <see cref="TaskResult.Done"/>. Prefer the factory
    /// overload to resolve dependencies once instead of rebuilding a context each frame.
    /// </summary>
    public static IServiceCollection AddFrameTask(
        this IServiceCollection services, string name, int sort,
        Func<FrameContext, TaskResult> run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return services.AddFrameTask(name, sort, provider =>
        {
            // native clock is always available, no AddClock dependency
            var clock = ClockObject.GetGlobalClock();
            var lifetime = provider.GetRequiredService<IHostApplicationLifetime>();
            return () => run(new FrameContext(provider, clock.GetDt(), lifetime.ApplicationStopping)) == TaskResult.Continue;
        });
    }
}

/// <summary>One registered per-frame task: its name, sort, and start-time tick factory.</summary>
internal sealed record FrameTaskSpec(string Name, int Sort, Func<IServiceProvider, Func<bool>> Tick);

/// <summary>
/// Brackets every <see cref="FrameTaskSpec"/> to the host lifetime: builds each task's callback from DI
/// on start and disposes all of them on stop.
/// </summary>
internal sealed class FrameTaskHost : IHostedService
{
    readonly IServiceProvider _provider;
    readonly IEnumerable<FrameTaskSpec> _specs;
    readonly List<PandaFrameTask> _tasks = new();

    public FrameTaskHost(IServiceProvider provider, IEnumerable<FrameTaskSpec> specs)
    {
        _provider = provider;
        _specs = specs;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var spec in _specs)
            _tasks.Add(PandaFrameTask.Register(spec.Name, spec.Sort, spec.Tick(_provider)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var task in _tasks)
            task.Dispose();
        _tasks.Clear();
        return Task.CompletedTask;
    }
}
