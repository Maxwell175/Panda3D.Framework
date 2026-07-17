using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Registration for the interval manager and its step task. Requires the event pump
/// (<c>AddEvents</c>) for awaitable completion via done-events.
/// </summary>
public static class IntervalsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IIntervalManager"/> and the step task at <see cref="FrameSlots.Intervals"/>.</summary>
    public static IServiceCollection AddIntervals(this IServiceCollection services)
    {
        services.TryAddSingleton<IntervalManager>();
        services.TryAddSingleton<IIntervalManager>(sp => sp.GetRequiredService<IntervalManager>());
        services.AddFrameTask("ivalLoop", FrameSlots.Intervals, sp =>
        {
            var manager = sp.GetRequiredService<IntervalManager>();
            return () => { manager.Step(); return true; };
        });
        return services;
    }
}
