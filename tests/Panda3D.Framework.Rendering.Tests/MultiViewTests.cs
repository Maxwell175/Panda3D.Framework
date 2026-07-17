using System;
using System.Linq;
using Interrogate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Rendering.Tests;

/// <summary>
/// The multi-view acceptance combinations (00 §7 / 03): two windows over independent scene roots
/// (DS-two-screens), two windows over one shared scene, and split-screen (one output, two regions).
/// Views are offscreen buffers so the whole thing runs headlessly; each renders every frame through
/// the igLoop, and a FrameTaskDiagnostics subscription fails the test if any RenderFrame faults.
/// </summary>
public sealed class MultiViewTests
{
    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    static bool SameNode(NodePath a, NodePath b) => a.Node().Equals(b.Node());
    static bool SameOutput(IGraphicsOutput a, IGraphicsOutput b) => a.Equals(b);
    static NodePath CameraScene(IView v) => v.Camera.Node.Node().CastTo<Camera>()!.GetScene();

    static ViewOptions Off(string? root = null, bool setup2d = true)
        => new() { SceneRoot = root, Offscreen = true, Setup2d = setup2d };

    /// <summary>Runs the given assertion body inside a real host loop, failing on any render fault.</summary>
    static void RunInLoop(Func<IServiceProvider, PandaTask> body)
    {
        string? renderError = null;
        void OnError(Exception ex) => renderError = ex.ToString();
        FrameTaskDiagnostics.UnhandledException += OnError;
        try
        {
            var builder = GameApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddSceneManager();
            builder.Services.AddEvents();
            builder.Services.AddClock();
            builder.Services.AddScheduler();
            builder.Services.AddRendering();     // igLoop renders every open output; no AddWindow (views opened manually)
            builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => body(sp)));

            var app = builder.Build();
            app.Run();
        }
        finally
        {
            FrameTaskDiagnostics.UnhandledException -= OnError;
        }
        Assert.Null(renderError);
    }

    static async PandaTask CloseAllAndStop(IViewManager views, IHostApplicationLifetime life)
    {
        for (int i = 0; i < 8; i++) await PandaTask.NextFrame();   // render a few frames of every output
        foreach (var v in views.Views.ToArray())
            views.CloseView(v);
        life.StopApplication();
    }

    [Fact]
    public void TwoWindows_TwoIndependentSceneRoots()
    {
        bool rootsDistinct = false, outputsDistinct = false, v1Top = false, v2Bottom = false, isolated = false;

        RunInLoop(async sp =>
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();

            var top = scene.GetRoot("top");
            var bottom = scene.GetRoot("bottom");
            rootsDistinct = !SameNode(top, bottom);
            top.AttachNewNode("marker-top");     // lives only in the "top" graph

            var v1 = views.OpenView(Off("top"));
            var v2 = views.OpenView(Off("bottom"));
            outputsDistinct = !SameOutput(v1.Output, v2.Output);
            v1Top = SameNode(CameraScene(v1), top);
            v2Bottom = SameNode(CameraScene(v2), bottom);
            // The marker is reachable from top's camera scene but NOT bottom's — the graphs are independent.
            isolated = !CameraScene(v1).Find("marker-top").IsEmpty()
                       && CameraScene(v2).Find("marker-top").IsEmpty();

            await CloseAllAndStop(views, life);
        });

        Assert.True(rootsDistinct, "GetRoot(top) and GetRoot(bottom) must be different nodes");
        Assert.True(outputsDistinct, "two windows must have distinct outputs");
        Assert.True(v1Top, "view 1's camera must render the 'top' root");
        Assert.True(v2Bottom, "view 2's camera must render the 'bottom' root");
        Assert.True(isolated, "a node in 'top' must not be visible from the 'bottom' graph");
    }

    [Fact]
    public void TwoWindows_OneSharedScene()
    {
        bool outputsDistinct = false, bothShared = false, bothSeeMarker = false;

        RunInLoop(async sp =>
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();

            scene.Root.AttachNewNode("shared-marker");

            var v1 = views.OpenView(Off());   // SceneRoot null = shared render root
            var v2 = views.OpenView(Off());
            outputsDistinct = !SameOutput(v1.Output, v2.Output);
            bothShared = SameNode(CameraScene(v1), scene.Root) && SameNode(CameraScene(v2), scene.Root);
            bothSeeMarker = !CameraScene(v1).Find("shared-marker").IsEmpty()
                            && !CameraScene(v2).Find("shared-marker").IsEmpty();

            await CloseAllAndStop(views, life);
        });

        Assert.True(outputsDistinct, "two windows must have distinct outputs");
        Assert.True(bothShared, "both cameras must render the shared render root");
        Assert.True(bothSeeMarker, "both views must see a node in the shared scene");
    }

    [Fact]
    public void OneWindow_TwoRegions_SplitScreen()
    {
        int regionCount = 0;
        bool splitVertically = false;

        RunInLoop(async sp =>
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();

            var view = views.OpenView(Off(setup2d: false));   // one 3-D region to start

            // Player 2's camera over the same scene, its own display region on the bottom half.
            var p2cam = new Camera("p2", new PerspectiveLens());
            p2cam.SetScene(scene.Root);
            var p2np = scene.Root.AttachNewNode(p2cam);

            view.Regions[0].SetDimensions(0f, 1f, 0.5f, 1f);          // player 1 = top half
            var bottom = view.AddRegion(new DisplayRegionOptions      // player 2 = bottom half
            {
                Dimensions = (0f, 1f, 0f, 0.5f),
                Sort = 1,
                Camera = p2np,
            });

            regionCount = view.Regions.Count;
            splitVertically = Math.Abs(view.Regions[0].GetBottom() - 0.5f) < 1e-4f
                              && Math.Abs(bottom.GetTop() - 0.5f) < 1e-4f
                              && Math.Abs(bottom.GetBottom() - 0f) < 1e-4f;

            await CloseAllAndStop(views, life);
        });

        Assert.Equal(2, regionCount);
        Assert.True(splitVertically, "the two regions must split the output top/bottom");
    }

    [Fact]
    public void RuntimeOpenAndClose()
    {
        int afterOpen = -1, afterClose = -1;

        RunInLoop(async sp =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();

            var a = views.OpenView(Off());
            var b = views.OpenView(Off());
            afterOpen = views.Views.Count;

            for (int i = 0; i < 4; i++) await PandaTask.NextFrame();

            views.CloseView(a);
            afterClose = views.Views.Count;
            Assert.True(a.IsClosed, "a closed view reports IsClosed");

            for (int i = 0; i < 4; i++) await PandaTask.NextFrame();   // b keeps rendering fine

            views.CloseView(b);
            life.StopApplication();
        });

        Assert.Equal(2, afterOpen);
        Assert.Equal(1, afterClose);
    }
}
