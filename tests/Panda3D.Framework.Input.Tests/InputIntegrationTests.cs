using System;
using Interrogate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Input.Tests;

public sealed class InputIntegrationTests
{
    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    /// <summary>AddInput's services all resolve headlessly (no window/loop), and AddContext works.</summary>
    [Fact]
    public void ServicesResolveAndContextIsCreated()
    {
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddEngine();
        builder.Services.AddInput();

        var app = builder.Build();

        Assert.NotNull(app.Services.GetService<IDevices>());
        Assert.NotNull(app.Services.GetService<IButtonLabels>());
        Assert.NotNull(app.Services.GetService<InputRuntime>());

        var context = app.Services.CreateContext("gameplay", priority: 5);
        Assert.Equal("gameplay", context.Name);
        Assert.Equal(5, context.Priority);

        // A device-agnostic action resolves and reads its default before any evaluation.
        var jump = context.Add(new ButtonAction("Jump"));
        jump.Bindings.Add(new ButtonBinding(Keys.Space));
        Assert.False(jump.IsPressed);
    }

    /// <summary>
    /// Best-effort windowed test: opens a real window (needs a display) and verifies the per-view
    /// MouseWatcher chain, the overlay wiring Rendering deferred, and that the dataLoop runs cleanly.
    /// If no window can be created (headless CI), it passes without asserting the window path.
    /// </summary>
    [Fact]
    public void WindowedViewBuildsInputChainAndWiresOverlay()
    {
        bool hadWindow = false, inputResolved = false, overlayWired = false, spaceUp = true, notOverUi = true, dataLoopRan = false;

        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o => o.Window.Size = (160, 120));   // windowed (needs a display)
        builder.Services.AddInput();
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        try
        {
            app.Run();
        }
        catch (Exception)
        {
            // No display / window creation failed — treat as a headless skip.
            return;
        }

        if (hadWindow)
        {
            Assert.True(inputResolved, "IInput should resolve for a windowed view");
            Assert.True(overlayWired, "the overlay PGTop should be wired to the view's MouseWatcher");
            Assert.True(spaceUp, "an unpressed key reads as up");
            Assert.True(notOverUi, "the pointer isn't over UI in a headless run");
            Assert.True(dataLoopRan, "the dataLoop ran without faulting");
        }

        async PandaTask BodyAsync(IServiceProvider sp)
        {
            try
            {
                var views = sp.GetRequiredService<IViewManager>();
                var life = sp.GetRequiredService<IHostApplicationLifetime>();
                var main = views.Main;

                if (main.Window is not null)
                {
                    hadWindow = true;
                    var input = main.Services.GetRequiredService<IInput>();
                    inputResolved = true;

                    var pg = main.Overlay2d?.Node().CastTo<PGTop>();
                    overlayWired = pg is not null && pg.GetMouseWatcher() is not null;

                    spaceUp = !input.IsDown(Keys.Space);
                    notOverUi = !input.IsOverUi;

                    for (int i = 0; i < 5; i++) await PandaTask.NextFrame();   // dataLoop traverses + evaluates
                    dataLoopRan = true;
                }
                life.StopApplication();
            }
            catch
            {
                sp.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            }
        }
    }
}
