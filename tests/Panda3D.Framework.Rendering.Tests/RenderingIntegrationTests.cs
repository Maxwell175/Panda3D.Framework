using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Panda3D.Framework.VisualTestSupport;
using Xunit;

namespace Panda3D.Framework.Rendering.Tests;

/// <summary>
/// Drives the real host loop headlessly with an OFFSCREEN view (no window popup): validates the full
/// rendering chain — engine/pipe/GSG, view + camera rig + 2-D overlay, the igLoop render task, and that
/// RenderFrame advances the clock (so AddClock's chain-tick is correctly suppressed, no double-advance).
/// </summary>
public sealed class RenderingIntegrationTests
{
    sealed class Probe
    {
        public bool WindowIsNull;
        public bool OutputPresent;
        public bool HasOverlay;
        public bool HasPixel2d;
        public bool HasOverlayAnchors;
        public bool OverlayAnchorPositions;
        public bool CameraWorks;
        public bool ClearColorRoundTrips;
        public bool AddedCameraSplitScreen;
        public bool FrameRateMeterTogglesRegion;
        public bool ClockAdvanced;
        public int BrightPixels;
        public int RegionCount;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void OffscreenViewRendersAndClockAdvancesViaRenderFrame()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();     // TickClock on, but AddRendering's IClockTickSource suppresses the chain tick
        builder.Services.AddScheduler();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o => o.Offscreen = true);
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        app.Run();

        Assert.True(probe.OutputPresent, "the view should have a graphics output");
        Assert.True(probe.WindowIsNull, "an offscreen view has no window");
        Assert.True(probe.HasOverlay, "Setup2d should create the aspect2d overlay");
        Assert.True(probe.HasPixel2d, "Setup2d should create the pixel2d root");
        Assert.True(probe.HasOverlayAnchors, "Setup2d should create the named overlay edge anchors");
        Assert.True(probe.OverlayAnchorPositions, "overlay edge anchors should sit at the aspect-correct edges");
        Assert.True(probe.CameraWorks, "camera rig perspective/orthographic should apply");
        Assert.True(probe.ClearColorRoundTrips, "IView.ClearColor should read back what was set on the native output");
        Assert.True(probe.AddedCameraSplitScreen, "AddCamera + AddRegion should wire a second camera into its own region");
        Assert.True(probe.FrameRateMeterTogglesRegion, "ShowFrameRate should add a display region and HideFrameRate remove it");
        Assert.True(probe.RegionCount >= 2, "expected the 3-D region plus the 2-D overlay region");
        Assert.True(probe.ClockAdvanced, "RenderFrame should advance the clock each rendered frame");
        Assert.True(probe.BrightPixels > 100, "rendered card should produce visible non-background pixels");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var views = sp.GetRequiredService<IViewManager>();
            var clock = sp.GetRequiredService<IGameClock>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var main = views.Main;

            // ClearColor reads back what was written to the native output (no cached copy).
            main.ClearColor = new LVecBase4f(0.1f, 0.2f, 0.3f, 1f);
            var read = main.ClearColor;
            probe.ClearColorRoundTrips =
                Math.Abs(read.GetX() - 0.1f) < 0.001f && Math.Abs(read.GetY() - 0.2f) < 0.001f
                && Math.Abs(read.GetZ() - 0.3f) < 0.001f && Math.Abs(read.GetW() - 1f) < 0.001f;

            // AddCamera unlocks a second rig; bind it to its own half-screen region.
            int rigsBefore = main.Cameras.Count;
            int regionsBefore = main.Regions.Count;
            var cam2 = main.AddCamera();
            main.AddRegion(new DisplayRegionOptions { Dimensions = (0.5f, 1f, 0f, 1f), Camera = cam2.Node });
            probe.AddedCameraSplitScreen =
                main.Cameras.Count == rigsBefore + 1
                && main.Regions.Count == regionsBefore + 1
                && !ReferenceEquals(cam2, main.Camera);

            // Frame-rate meter adds its own display region on the output; hiding removes it.
            int drBefore = main.Output.GetNumDisplayRegions();
            main.ShowFrameRate();
            int drWithMeter = main.Output.GetNumDisplayRegions();
            main.HideFrameRate();
            int drAfterHide = main.Output.GetNumDisplayRegions();
            probe.FrameRateMeterTogglesRegion = drWithMeter > drBefore && drAfterHide < drWithMeter;

            VisualTestHelpers.UseBlackBackground(main);
            probe.OutputPresent = main.Output is not null;
            probe.WindowIsNull = main.Window is null;
            probe.HasOverlay = main.Overlay2d is not null;
            probe.HasPixel2d = main.Pixel2d is not null;
            probe.HasOverlayAnchors = main.OverlayAnchors.Count == 9
                                      && main.OverlayAnchors.ContainsKey(OverlayAnchor.TopLeft)
                                      && main.OverlayAnchors.ContainsKey(OverlayAnchor.BottomRight);
            var output = main.Output ?? throw new InvalidOperationException("view output was not created");
            var ar = output.GetXSize() / (float)output.GetYSize();
            var edges = Overlay2dMath.OverlayEdges(ar);
            var topLeft = main.OverlayAnchors[OverlayAnchor.TopLeft];
            var bottomRight = main.OverlayAnchors[OverlayAnchor.BottomRight];
            probe.OverlayAnchorPositions =
                Math.Abs(topLeft.GetX() - edges.Left) < 0.001f
                && Math.Abs(topLeft.GetZ() - edges.Top) < 0.001f
                && Math.Abs(bottomRight.GetX() - edges.Right) < 0.001f
                && Math.Abs(bottomRight.GetZ() - edges.Bottom) < 0.001f;
            probe.RegionCount = main.Regions.Count;

            // Exercise the camera rig wrapper.
            main.Camera.SetPerspective(60f);
            main.Camera.SetOrthographic(2f, 2f);
            probe.CameraWorks = main.Camera.Lens is not null;

            var card = VisualTestHelpers.AddWhiteCard(sp.GetRequiredService<ISceneManager>().Root, "visible-card", 1.0f);
            card.SetY(5f);

            long f0 = clock.FrameCount;
            for (int i = 0; i < 12; i++)
                await PandaTask.NextFrame();     // igLoop renders each epoch; RenderFrame ticks the clock
            probe.ClockAdvanced = clock.FrameCount > f0;
            probe.BrightPixels = VisualTestHelpers.CountBrightPixels(VisualTestHelpers.Capture(main));

            life.StopApplication();
        }
    }
}
