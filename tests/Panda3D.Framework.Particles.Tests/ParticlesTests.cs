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
using Panda3D.Physics;
using Xunit;

namespace Panda3D.Framework.Particles.Tests;

public sealed class ParticlesTests
{
    sealed class Probe
    {
        public int Updates;
        public int ParticlePixels;
        public int LivingParticles;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void ParticleLoopUpdatesManagers()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddParticles();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(probe.Updates > 0, "particleLoop should call the particles update at FrameSlots.Collision");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var particles = sp.GetRequiredService<ParticlesService>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            for (int i = 0; i < 4; i++)
                await PandaTask.NextFrame();

            probe.Updates = particles.UpdateCount;
            life.StopApplication();
        }
    }

    [Fact]
    public void ParticleEffectOwnsSystemsAndRootNode()
    {
        using var particles = new ParticlesService();
        using var effect = particles.Create("sparks");
        var system = new ParticleSystem();

        effect.Systems.Add(system);

        Assert.Single(effect.Systems);
        Assert.Equal("sparks", effect.Node.GetName());
        Assert.Equal("sparks-system-0", system.GetRenderParent().GetName());
        Assert.False(effect.Node.Find("sparks-system-0").IsEmpty());

        effect.SoftStart();
        effect.SoftStop();

        effect.Systems.Remove(system);

        Assert.Empty(effect.Systems);
    }

    [Fact]
    public void ParticleIntervalParentsSoftStopsAndCleansUpEffect()
    {
        using var particles = new ParticlesService();
        var parent = new NodePath("particle-parent");
        var effect = particles.Create("timed");
        effect.Systems.Add(new ParticleSystem());

        var interval = new ParticleInterval(effect, parent, duration: 1.0, softStopT: 0.25, cleanup: true);

        interval.Initialize(0);
        Assert.True(effect.Node.GetParent().Node().Equals(parent.Node()));

        interval.Step(0.8);
        interval.Complete();

        Assert.True(effect.Node.IsEmpty(), "cleanup should dispose the effect root at the end of the interval");
    }

    [Fact]
    public void ParticleEffectRendersVisiblePixels()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddRendering();
        builder.Services.AddWindow(o =>
        {
            o.Offscreen = true;
            o.Setup2d = false;
        });
        builder.Services.AddParticles();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(
            probe.ParticlePixels > 0,
            $"configured ParticleEffect should render non-background pixels; living={probe.LivingParticles}");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var views = sp.GetRequiredService<IViewManager>();
            var particles = sp.GetRequiredService<IParticles>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var view = views.Main;
            VisualTestHelpers.UseBlackBackground(view);

            using var effect = particles.Create("visible-particles");
            effect.Node.ReparentTo(scene.Root);
            effect.Node.SetY(5f);

            var system = CreateVisibleParticleSystem();
            effect.Systems.Add(system);
            effect.SoftStart();
            system.SetActiveSystemFlag(true);
            system.BirthLitter();

            for (int i = 0; i < 20; i++)
            {
                system.BirthLitter();
                await PandaTask.NextFrame();
            }

            probe.LivingParticles = system.GetLivingParticles();
            probe.ParticlePixels = VisualTestHelpers.CountNonBlackPixels(VisualTestHelpers.Capture(view));
            life.StopApplication();
        }
    }

    static ParticleSystem CreateVisibleParticleSystem()
    {
        var system = new ParticleSystem(256);
        system.SetBirthRate(0.01f);
        system.SetLitterSize(96);
        system.SetLitterSpread(0);
        system.SetLocalVelocityFlag(true);
        system.SetActiveSystemFlag(true);

        var factory = new PointParticleFactory();
        factory.SetLifespanBase(5f);
        factory.SetLifespanSpread(0f);
        factory.SetMassBase(1f);
        factory.SetTerminalVelocityBase(0f);
        system.SetFactory(factory);

        var renderer = new PointParticleRenderer();
        renderer.SetPointSize(20f);
        renderer.SetStartColor(new LVecBase4f(1f, 1f, 1f, 1f));
        renderer.SetEndColor(new LVecBase4f(1f, 1f, 1f, 1f));
        renderer.SetBlendType(PointParticleRendererPointParticleBlendType.PpOneColor);
        renderer.SetBlendMethod(BaseParticleRendererParticleRendererBlendMethod.PpNoBlend);
        system.SetRenderer(renderer);

        var emitter = new PointEmitter();
        emitter.SetLocation(new LPoint3f(0f, 0f, 0f));
        emitter.SetEmissionType(BaseParticleEmitteremissionType.EtExplicit);
        emitter.SetExplicitLaunchVector(new LVector3f(0f, 0f, 0f));
        emitter.SetAmplitude(0f);
        emitter.SetAmplitudeSpread(0f);
        system.SetEmitter(emitter);

        return system;
    }
}
