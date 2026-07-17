using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;
using Panda3D.Framework.VisualTestSupport;
using Xunit;

namespace Panda3D.Framework.Intervals.Tests;

public sealed class IntervalVisualTests
{
    sealed class Probe
    {
        public int InitialLeftPixels;
        public int InitialRightPixels;
        public int FinalLeftPixels;
        public int FinalRightPixels;
        public double FinalX;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void HostDrivenPosIntervalMovesRenderedPixelsAcrossTheFrame()
    {
        var clock = ClockObject.GetGlobalClock();
        clock.Reset();
        clock.SetMode(ClockObjectMode.MNonRealTime);
        clock.SetDt(0.05);

        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddIntervals();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o =>
        {
            o.Offscreen = true;
            o.Setup2d = false;
        });
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        try
        {
            app.Run();
        }
        finally
        {
            clock.SetMode(ClockObjectMode.MNormal);
            clock.Reset();
        }

        Assert.True(
            probe.InitialLeftPixels > 100,
            $"card should initially render in the left half (left={probe.InitialLeftPixels}, right={probe.InitialRightPixels})");
        Assert.True(
            probe.InitialLeftPixels > probe.InitialRightPixels * 4,
            $"initial image should be left-weighted (left={probe.InitialLeftPixels}, right={probe.InitialRightPixels})");
        Assert.True(
            probe.FinalRightPixels > 100,
            $"card should render in the right half after interval playback (left={probe.FinalLeftPixels}, right={probe.FinalRightPixels})");
        Assert.True(
            probe.FinalRightPixels > probe.FinalLeftPixels * 4,
            $"final image should be right-weighted (left={probe.FinalLeftPixels}, right={probe.FinalRightPixels})");
        Assert.True(probe.FinalX > 0.9, $"node should have reached the interval target (x={probe.FinalX})");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var intervals = sp.GetRequiredService<IIntervalManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var view = views.Main;
            VisualTestHelpers.UseBlackBackground(view);
            view.Camera.SetOrthographic(4f, 2.25f);

            var card = VisualTestHelpers.AddWhiteCard(scene.Root, "interval-card", 0.5f);
            card.SetPos(new LVecBase3f(-1.2f, 5f, 0f));

            await PandaTask.NextFrame();
            CaptureHalves(view, out probe.InitialLeftPixels, out probe.InitialRightPixels);

            intervals.Play(card.PosTo(new LVecBase3f(1.2f, 5f, 0f), 0.15, from: new LVecBase3f(-1.2f, 5f, 0f)));
            for (int i = 0; i < 8; i++)
                await PandaTask.NextFrame();

            probe.FinalX = card.GetX();
            CaptureHalves(view, out probe.FinalLeftPixels, out probe.FinalRightPixels);
            life.StopApplication();
        }

        static void CaptureHalves(IView view, out int leftPixels, out int rightPixels)
        {
            var image = VisualTestHelpers.Capture(view);
            int width = image.GetXSize();
            leftPixels = VisualTestHelpers.CountBrightPixelsInColumns(image, 0, width / 2);
            rightPixels = VisualTestHelpers.CountBrightPixelsInColumns(image, width / 2, width);
        }
    }
}
