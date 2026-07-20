using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Interrogate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Intervals;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;
using Panda3D.Framework.VisualTestSupport;
using Xunit;

namespace Panda3D.Framework.Actors.Tests;

public sealed class ActorTests
{
    static readonly string RalphDir = Path.Combine(AppContext.BaseDirectory, "models");
    static readonly string RalphModel = Path.Combine(RalphDir, "ralph.egg.pz");
    static readonly string RalphWalk = Path.Combine(RalphDir, "ralph-walk.egg.pz");
    static readonly string RalphRun = Path.Combine(RalphDir, "ralph-run.egg.pz");

    sealed class Probe
    {
        public int RalphPixels;
        public int PoseDifferencePixels;
        public int LodSwitches;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void LoadsRoamingRalphAndBindsNamedAnimations()
    {
        using var actor = LoadRalph();

        Assert.Contains("walk", actor.Anims);
        Assert.Contains("run", actor.Anims);
        Assert.True(actor.Character.GetNumBundles() > 0, "Ralph should load with a character bundle");

        var walk = actor.Anim("walk");
        Assert.True(walk.HasAnim(), "Ralph's walk animation should bind");
        Assert.True(walk.GetNumFrames() > 0, "walk animation should expose frames");
        Assert.True(walk.GetFrameRate() > 0, "walk animation should expose its native frame rate");

        walk.Loop(true);
        Assert.True(walk.IsPlaying(), "native AnimControl should be the playback handle");
        walk.Stop();
    }

    [Fact]
    public void AnimClipOverloadLoadsAndTryAnimGuardsUnknownClips()
    {
        using var actor = new ActorLoader().Load(
            RalphModel,
            new AnimClip("walk", RalphWalk),
            new AnimClip("run", RalphRun));

        Assert.True(actor.TryAnim("walk", out var walk));
        Assert.NotNull(walk);
        Assert.True(walk!.HasAnim());

        Assert.False(actor.TryAnim("missing", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void BlendHelpersSetNativePartBundleWeights()
    {
        using var actor = LoadRalph();
        var walk = actor.Anim("walk");
        var run = actor.Anim("run");

        actor.EnableBlend();
        actor.SetBlendWeight("walk", 0.25f);
        actor.SetBlendWeight("run", 0.75f);

        Assert.True(actor.Rig.Part().GetAnimBlendFlag());
        Assert.Equal(0.25f, actor.Rig.Part().GetControlEffect(walk), 3);
        Assert.Equal(0.75f, actor.Rig.Part().GetControlEffect(run), 3);
    }

    [Fact]
    public async Task LoadsMultipartRalphPartsIndependently()
    {
        var parts = new Dictionary<string, ActorPart>
        {
            ["torso"] = new(RalphModel, new Dictionary<string, string> { ["walk"] = RalphWalk }),
            ["legs"] = new(RalphModel, new Dictionary<string, string> { ["run"] = RalphRun }),
        };

        using var actor = await new ActorLoader().LoadAsync(parts);

        Assert.Contains("walk", actor.Anims);
        Assert.Contains("run", actor.Anims);
        Assert.True(actor.Anim("walk", "torso").HasAnim());
        Assert.True(actor.Anim("run", "legs").HasAnim());
        // Distinct native bundles. Compare identity, not value: PartBundle now
        // implements IReadOnlyList<PartBundleNode>, so Assert.NotEqual would do a
        // structural sequence comparison rather than a reference check.
        Assert.NotSame(actor.Rig.Part("torso"), actor.Rig.Part("legs"));
        Assert.Throws<KeyNotFoundException>(() => actor.Rig.Part());
        Assert.Throws<KeyNotFoundException>(() => actor.Anim("walk", "legs"));
    }

    [Fact]
    public void ActorIntervalPosesRalphAnimationFromTimelineTime()
    {
        using var actor = LoadRalph();
        var walk = actor.Anim("walk");
        double rateScale = 10.0 / walk.GetFrameRate();
        var interval = new ActorInterval(actor, "walk", duration: 1.0, startFrame: 0, endFrame: 10, playRate: rateScale);

        interval.Step(0.5);

        Assert.Equal(5.0, walk.GetFullFframe(), 1);
        Assert.False(walk.IsPlaying(), "ActorInterval should pose the animation rather than free-running it");

        interval.Complete();
        Assert.Equal(10.0, walk.GetFullFframe(), 1);
    }

    [Fact]
    public void CrossFadeComposesNativeAnimEffectInterval()
    {
        var probe = new BlendProbe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddIntervals();
        builder.Services.AddActors();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.Equal(0f, probe.WalkWeight, 3);
        Assert.Equal(1f, probe.RunWeight, 3);

        static PandaTask BodyAsync(IServiceProvider sp)
        {
            var actors = sp.GetRequiredService<IActorLoader>();
            var intervals = sp.GetRequiredService<IIntervalManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<BlendProbe>();

            using var actor = actors.Load(RalphModel, RalphAnims());
            actor.EnableBlend();
            actor.SetBlendWeight("walk", 1f);
            actor.SetBlendWeight("run", 0f);

            var handle = intervals.Play(actor.CrossFade("walk", "run", 0.25));
            handle.Time = 0.25;
            probe.WalkWeight = actor.Rig.Part().GetControlEffect(actor.Anim("walk"));
            probe.RunWeight = actor.Rig.Part().GetControlEffect(actor.Anim("run"));
            life.StopApplication();
            return PandaTask.CompletedTask;
        }
    }

    [Fact]
    public void RoamingRalphRendersAndAnimationPoseChangesPixels()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o =>
        {
            o.Offscreen = true;
            o.Setup2d = false;
        });
        builder.Services.AddActors();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(probe.RalphPixels > 500, $"Ralph should render visible non-background pixels (was {probe.RalphPixels})");
        Assert.True(probe.PoseDifferencePixels > 25, $"different animation poses should alter rendered pixels (was {probe.PoseDifferencePixels})");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var actors = sp.GetRequiredService<IActorLoader>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var view = views.Main;
            VisualTestHelpers.UseBlackBackground(view);
            view.Camera.SetPerspective(45f);

            using var actor = actors.Load(RalphModel, RalphAnims());
            actor.Node.ReparentTo(scene.Root);
            actor.Node.SetPos(0f, 8f, -2.2f);
            actor.Node.SetScale(0.25f);
            actor.Node.SetH(180f);
            actor.Node.SetLightOff();

            var walk = actor.Anim("walk");
            walk.Pose(0);
            actor.Character.ForceUpdate();
            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            var first = VisualTestHelpers.Capture(view);
            probe.RalphPixels = VisualTestHelpers.CountNonBlackPixels(first);

            walk.Pose(Math.Min(15, walk.GetNumFrames() - 1));
            actor.Character.ForceUpdate();
            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            var second = VisualTestHelpers.Capture(view);
            probe.PoseDifferencePixels = VisualTestHelpers.CountDifferentPixels(first, second);
            life.StopApplication();
        }
    }

    [Fact]
    public void MergedLodRalphRendersForcedFarAnimationPoseChangesPixels()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddScheduler();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o =>
        {
            o.Offscreen = true;
            o.Setup2d = false;
        });
        builder.Services.AddActors();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.Equal(2, probe.LodSwitches);
        Assert.True(probe.RalphPixels > 500, $"forced far LOD Ralph should render visible non-background pixels (was {probe.RalphPixels})");
        Assert.True(probe.PoseDifferencePixels > 25, $"posing the merged far LOD should alter rendered pixels (was {probe.PoseDifferencePixels})");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var actors = sp.GetRequiredService<IActorLoader>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var view = views.Main;
            VisualTestHelpers.UseBlackBackground(view);
            view.Camera.SetPerspective(45f);

            using var actor = await actors.LoadAsync(RalphLodDefinition());
            probe.LodSwitches = actor.Rig.LodNode?.GetNumSwitches() ?? 0;
            actor.Rig.LodNode?.ForceSwitch(1);
            actor.Node.ReparentTo(scene.Root);
            actor.Node.SetPos(0f, 8f, -2.2f);
            actor.Node.SetScale(0.25f);
            actor.Node.SetH(180f);
            actor.Node.SetLightOff();

            var walk = actor.Anim("walk");
            walk.Pose(0);
            ForceAllCharacters(actor);
            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            var first = VisualTestHelpers.Capture(view);
            probe.RalphPixels = VisualTestHelpers.CountNonBlackPixels(first);

            walk.Pose(Math.Min(15, walk.GetNumFrames() - 1));
            ForceAllCharacters(actor);
            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            var second = VisualTestHelpers.Capture(view);
            probe.PoseDifferencePixels = VisualTestHelpers.CountDifferentPixels(first, second);
            life.StopApplication();
        }
    }

    static IActor LoadRalph() => new ActorLoader().Load(RalphModel, RalphAnims());

    static IReadOnlyDictionary<string, string> RalphAnims() => new Dictionary<string, string>
    {
        ["walk"] = RalphWalk,
        ["run"] = RalphRun,
    };

    static ActorDefinition RalphLodDefinition()
    {
        var definition = new ActorDefinition();
        definition.Lods.Add(new LodLevel("near", 100f, 20f));
        definition.Lods.Add(new LodLevel("far", 20f, 0f));

        var part = new ActorPartDef();
        part.ModelByLod["near"] = RalphModel;
        part.ModelByLod["far"] = RalphModel;
        part.Anims["walk"] = RalphWalk;
        part.Anims["run"] = RalphRun;
        definition.Parts[ActorDefaults.DefaultPart] = part;
        return definition;
    }

    static void ForceAllCharacters(IActor actor)
    {
        var characters = actor.Node.FindAllMatches("**/+Character");
        for (int i = 0; i < characters.GetNumPaths(); i++)
            characters.GetPath(i).Node().CastTo<Character>()?.ForceUpdate();
    }

    sealed class BlendProbe
    {
        public float WalkWeight;
        public float RunWeight;
    }
}
