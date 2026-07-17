using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Panda3D.Framework.Actors;

public static class ActorsServiceCollectionExtensions
{
    public static IServiceCollection AddActors(this IServiceCollection services)
    {
        services.TryAddSingleton<ActorLoader>();
        services.TryAddSingleton<IActorLoader>(sp => sp.GetRequiredService<ActorLoader>());
        return services;
    }
}
