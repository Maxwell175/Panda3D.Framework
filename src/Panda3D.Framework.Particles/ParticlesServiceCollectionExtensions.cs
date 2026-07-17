using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Particles;

/// <summary>Registration for the built-in physics and particle managers.</summary>
public static class ParticlesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in physics and particle managers (<see cref="IParticles"/>) plus a per-frame
    /// update task at <see cref="FrameSlots.Collision"/>.
    /// </summary>
    public static IServiceCollection AddParticles(this IServiceCollection services)
    {
        services.TryAddSingleton<ParticlesService>();
        services.TryAddSingleton<IParticles>(sp => sp.GetRequiredService<ParticlesService>());
        services.AddFrameTask("particleLoop", FrameSlots.Collision, sp =>
        {
            var particles = sp.GetRequiredService<ParticlesService>();
            var clock = sp.GetRequiredService<IGameClock>();
            return () => { particles.Update((float)clock.Dt); return true; };
        });
        return services;
    }
}
