using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Audio;

/// <summary>Registration for Panda audio managers, 3-D audio tracking, and the audio update task.</summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAudio"/> and the <c>audioLoop</c> task at <see cref="FrameSlots.Audio"/>.
    /// Requires <c>AddEvents</c> so finished events can be observed.
    /// </summary>
    public static IServiceCollection AddAudio(this IServiceCollection services)
    {
        services.TryAddSingleton<AudioService>();
        services.TryAddSingleton<IAudio>(sp => sp.GetRequiredService<AudioService>());
        services.AddFrameTask("audioLoop", FrameSlots.Audio, sp =>
        {
            var audio = sp.GetRequiredService<AudioService>();
            var clock = sp.GetRequiredService<IGameClock>();
            return () => { audio.Update(clock.Dt); return true; };
        });
        return services;
    }

    /// <summary>Registers a scoped <see cref="IAudio3D"/> attach registry over the SFX manager.</summary>
    public static IServiceCollection AddAudio3D(this IServiceCollection services)
    {
        services.AddAudio();
        services.TryAddScoped<IAudio3D, Audio3D>();
        return services;
    }
}
