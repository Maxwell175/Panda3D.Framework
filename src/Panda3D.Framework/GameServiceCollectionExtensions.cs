using System;
using Microsoft.Extensions.DependencyInjection;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Input;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework;

/// <summary>One-call registration of the standard windowed-game services.</summary>
public static class GameServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scene, events, clock, scheduler, rendering, a window, and input — the core of a
    /// windowed game. Add features (<c>AddActors</c>, <c>AddCollision</c>, <c>AddIntervals</c>, …) on top.
    /// </summary>
    public static IServiceCollection AddGame(this IServiceCollection services, Action<ViewOptions>? window = null)
    {
        services.AddSceneManager();
        services.AddEvents();
        services.AddClock();
        services.AddScheduler();
        services.AddRendering();
        services.AddWindow(window);
        services.AddInput();
        return services;
    }
}
