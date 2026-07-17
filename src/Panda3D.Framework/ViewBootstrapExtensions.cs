using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Panda3D.Async;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework;

/// <summary>Bootstrap variants that run in the main view's scope, so per-view services inject directly.</summary>
public static class ViewBootstrapExtensions
{
    /// <summary>
    /// Register the entry coroutine in the main view's scope. The provided <see cref="IServiceProvider"/>
    /// is that scope, so it resolves per-view services (<c>IInput</c>, <c>IGui</c>) as well as root singletons.
    /// </summary>
    public static IServiceCollection AddViewBootstrap(this IServiceCollection services, Func<IServiceProvider, PandaTask> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return services.AddBootstrap(root => run(root.GetRequiredService<IViewManager>().Main.Services));
    }

    /// <summary>
    /// Register the entry coroutine in the main view's scope, with its services injected as parameters —
    /// including per-view services such as <c>IInput</c>. Bound by reflection at startup; a NativeAOT build
    /// should use the <see cref="IServiceProvider"/> overload.
    /// </summary>
    [RequiresUnreferencedCode("The delegate's parameter types are resolved from DI by reflection.")]
    [RequiresDynamicCode("The delegate is invoked by reflection.")]
    public static IServiceCollection AddViewBootstrap(this IServiceCollection services, Delegate run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return services.AddViewBootstrap(scope =>
        {
            var parameters = run.Method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                args[i] = scope.GetRequiredService(parameters[i].ParameterType);
            return run.DynamicInvoke(args) switch
            {
                PandaTask task => task,
                Task task => task.ToPandaTask(),
                null => Task.CompletedTask.ToPandaTask(),
                var other => throw new InvalidOperationException(
                    $"A bootstrap delegate must return PandaTask or Task, not {other.GetType()}."),
            };
        });
    }
}
