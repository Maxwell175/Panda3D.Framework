using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Core;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Input;

/// <summary>
/// Registration for the data graph, devices, the action runtime, and the <c>dataLoop</c>.
/// </summary>
public static class InputServiceCollectionExtensions
{
    /// <summary>
    /// Registers input services and the <c>dataLoop</c> task at <see cref="FrameSlots.DataLoop"/>: each epoch
    /// it rolls the per-frame edge snapshots, traverses the data graph, then evaluates all actions.
    /// </summary>
    public static IServiceCollection AddInput(this IServiceCollection services)
    {
        services.TryAddSingleton<DataGraph>();
        services.TryAddSingleton<InputRegistry>();
        services.TryAddSingleton<InputRuntime>();
        services.TryAddSingleton<IDevices, Devices>();
        services.TryAddSingleton<IButtonLabels, ButtonLabels>();
        services.TryAddScoped<IInput, ViewInput>();

        services.AddFrameTask("dataLoop", FrameSlots.DataLoop, sp =>
        {
            var runtime = sp.GetRequiredService<InputRuntime>();
            var dataGraph = sp.GetRequiredService<DataGraph>();
            var registry = sp.GetRequiredService<InputRegistry>();
            var views = sp.GetRequiredService<IViewManager>();
            var clock = sp.GetRequiredService<IGameClock>();

            // keyboard/mouse source is the main view's watcher, resolved lazily once it exists
            ViewInput? mainInput = null;
            IMouseWatcher? MainWatcher()
            {
                if (mainInput is null && views.MainOrNull is { Window: not null } main)
                    mainInput = main.Services.GetService<IInput>() as ViewInput;
                return mainInput?.Watcher;
            }

            runtime.SetSampler(new DeviceSampler(MainWatcher, InputDeviceManager.GetGlobalPtr()));
            return () =>
            {
                registry.CaptureAll();   // snapshot last-frame state before the traversal updates it
                dataGraph.Traverse();    // pull new OS events into the watchers
                runtime.Evaluate((float)clock.Dt);
                return true;
            };
        });
        return services;
    }

    /// <summary>Create a named, prioritized input context on the runtime resolved from this provider.</summary>
    public static IInputContext CreateContext(this IServiceProvider provider, string name, int priority = 0)
        => provider.GetRequiredService<InputRuntime>().AddContext(name, priority);
}
