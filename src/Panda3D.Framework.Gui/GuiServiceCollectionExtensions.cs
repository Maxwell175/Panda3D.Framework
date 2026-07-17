using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Panda3D.Framework.Gui;

/// <summary>Registration for the per-view GUI scope.</summary>
public static class GuiServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGui"/> as a scoped view service. Requires rendering and events to be
    /// configured by the application.
    /// </summary>
    public static IServiceCollection AddGui(this IServiceCollection services)
    {
        services.TryAddScoped<IGui, GuiService>();
        return services;
    }
}
