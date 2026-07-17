using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Panda3D.Framework.Hosting;

/// <summary>
/// The ASP.NET-style builder for a <see cref="GameApplication"/>: configure services, configuration
/// and logging, then <see cref="Build"/>.
/// </summary>
public interface IGameApplicationBuilder
{
    /// <summary>The service collection composed by the app's <c>Program.cs</c>.</summary>
    IServiceCollection Services { get; }

    /// <summary>The layered configuration (env, JSON, command line).</summary>
    IConfigurationManager Configuration { get; }

    /// <summary>Materialize the application. Starts nothing yet — call <see cref="GameApplication.Run"/>.</summary>
    GameApplication Build();
}

/// <summary>
/// Wraps <see cref="HostApplicationBuilder"/> so a game gets configuration, logging, options,
/// <c>IHostApplicationLifetime</c> and <c>IHostedService</c> for free.
/// </summary>
internal sealed class GameApplicationBuilder : IGameApplicationBuilder
{
    readonly HostApplicationBuilder _inner;

    public GameApplicationBuilder(string[] args)
    {
        _inner = Host.CreateApplicationBuilder(args);
    }

    public IServiceCollection Services => _inner.Services;
    public IConfigurationManager Configuration => _inner.Configuration;

    public GameApplication Build() => new(_inner.Build());
}
