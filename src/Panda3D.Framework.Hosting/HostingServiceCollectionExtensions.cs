using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Async;

namespace Panda3D.Framework.Hosting;

/// <summary>Bootstrap and scene-manager registration extensions.</summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>Register the app's entry coroutine, spawned at the gameplay slot when <c>Run</c> starts.</summary>
    public static IServiceCollection AddBootstrap<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceCollection services)
        where T : class, IBootstrap
    {
        services.AddSingleton<IBootstrap, T>();
        return services;
    }

    /// <summary>
    /// Register the app's entry coroutine inline as a delegate (resolve services from the provider),
    /// spawned at the gameplay slot when <c>Run</c> starts.
    /// </summary>
    public static IServiceCollection AddBootstrap(this IServiceCollection services, Func<IServiceProvider, PandaTask> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(sp, run));
        return services;
    }

    /// <summary>
    /// Register the entry coroutine inline, with any number of services injected as parameters
    /// (ASP.NET-minimal-API style). The delegate may return <see cref="PandaTask"/> or <see cref="Task"/>.
    /// Parameters are resolved by reflection at startup; a NativeAOT build should prefer the
    /// <see cref="AddBootstrap(IServiceCollection, Func{IServiceProvider, PandaTask})"/> overload.
    /// </summary>
    [RequiresUnreferencedCode("The delegate's parameter types are resolved from DI by reflection.")]
    [RequiresDynamicCode("The delegate is invoked by reflection.")]
    public static IServiceCollection AddBootstrap(this IServiceCollection services, Delegate run)
    {
        ArgumentNullException.ThrowIfNull(run);
        services.AddSingleton<IBootstrap>(sp => new InjectedBootstrap(sp, run));
        return services;
    }

    sealed class DelegateBootstrap(IServiceProvider services, Func<IServiceProvider, PandaTask> run) : IBootstrap
    {
        public PandaTask RunAsync() => run(services);
    }

    sealed class InjectedBootstrap(IServiceProvider services, Delegate run) : IBootstrap
    {
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Guarded by AddBootstrap(Delegate).")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded by AddBootstrap(Delegate).")]
        public PandaTask RunAsync()
        {
            var parameters = run.Method.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                args[i] = services.GetRequiredService(parameters[i].ParameterType);
            return run.DynamicInvoke(args) switch
            {
                PandaTask task => task,
                Task task => task.ToPandaTask(),
                null => Task.CompletedTask.ToPandaTask(),
                var other => throw new InvalidOperationException(
                    $"A bootstrap delegate must return PandaTask or Task, not {other.GetType()}."),
            };
        }
    }

    /// <summary>Register <see cref="ISceneManager"/> — the world-root service.</summary>
    public static IServiceCollection AddSceneManager(this IServiceCollection services)
    {
        services.TryAddSingleton<ISceneManager, SceneManager>();
        return services;
    }
}
