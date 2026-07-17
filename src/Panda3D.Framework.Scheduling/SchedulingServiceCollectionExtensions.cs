using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Panda3D.Core;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// Registration for the clock (pacing + ticking) and the gameplay scheduler.
/// </summary>
public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGameClock"/> and the clock configuration (pacing + tick source). See
    /// <see cref="ClockOptions"/>: keep <c>TickClock</c> on for headless, off when <c>RenderFrame</c> ticks.
    /// </summary>
    public static IServiceCollection AddClock(this IServiceCollection services, Action<ClockOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);
        services.TryAddSingleton<IGameClock, GameClock>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ClockConfigurator>());
        return services;
    }

    /// <summary>Registers <see cref="IFrameScheduler"/> (and a clock for it to read).</summary>
    public static IServiceCollection AddScheduler(this IServiceCollection services)
    {
        services.TryAddSingleton<IGameClock, GameClock>();
        services.TryAddSingleton<IFrameScheduler, FrameScheduler>();
        return services;
    }
}

/// <summary>Applies clock pacing and the ticking-chain choice at host start.</summary>
internal sealed class ClockConfigurator : IHostedService
{
    readonly ClockOptions _options;
    readonly IServiceProvider _provider;

    public ClockConfigurator(IOptions<ClockOptions> options, IServiceProvider provider)
    {
        _options = options.Value;
        _provider = provider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var clock = ClockObject.GetGlobalClock();

        if (_options.LimitFrameRate)
        {
            clock.SetMode(ClockObjectMode.MLimited);
            clock.SetFrameRate(_options.MaxFps);
        }
        if (_options.MaxDt > 0)
            clock.SetMaxDt(_options.MaxDt);

        // tick from the default chain only when nothing else does — RenderFrame registers an
        // IClockTickSource, headless registers none; avoids a double-advance
        if (_options.TickClock && _provider.GetService(typeof(IClockTickSource)) is null)
        {
            var chain = AsyncTaskManager.GetGlobalPtr().FindTaskChain("default");
            chain?.SetTickClock(true);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
