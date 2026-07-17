using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Events;

/// <summary>Registration for the event pump and named bus.</summary>
public static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the single event pump (<see cref="INamedEventBus"/>) and its per-frame drain task.
    /// Idempotent — calling twice does not create a second drainer, preserving the single-pump invariant.
    /// </summary>
    public static IServiceCollection AddEvents(this IServiceCollection services)
    {
        services.TryAddSingleton<EventPump>();
        services.TryAddSingleton<INamedEventBus>(sp => sp.GetRequiredService<EventPump>());
        services.AddFrameTask("eventManager", FrameSlots.Events, sp =>
        {
            var pump = sp.GetRequiredService<EventPump>();
            return () => { pump.PumpOnce(); return true; };
        });
        return services;
    }
}
