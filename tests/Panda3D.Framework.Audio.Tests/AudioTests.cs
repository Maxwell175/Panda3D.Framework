using System;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Xunit;

namespace Panda3D.Framework.Audio.Tests;

public sealed class AudioTests
{
    sealed class Probe
    {
        public int AudioUpdates;
        public int SpatialUpdates;
        public bool FinishedAwaited;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    static IGameApplicationBuilder NewBuilder(Probe probe)
    {
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddAudio3D();
        builder.Services.AddSingleton(probe);
        return builder;
    }

    [Fact]
    public void AudioLoopUpdatesNativeManagers()
    {
        var probe = new Probe();
        var builder = NewBuilder(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(probe.AudioUpdates > 0, "audioLoop should call the AudioService update at FrameSlots.Audio");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var audio = sp.GetRequiredService<AudioService>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            for (int i = 0; i < 4; i++)
                await PandaTask.NextFrame();

            probe.AudioUpdates = audio.UpdateCount;
            life.StopApplication();
        }
    }

    [Fact]
    public void SoundFinishedArmsNativeEventAndCompletesForStoppedSound()
    {
        var probe = new Probe();
        var builder = NewBuilder(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(probe.FinishedAwaited);

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var audio = sp.GetRequiredService<IAudio>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var sound = audio.Wrap(audio.Sfx.GetNullSound());
            Assert.NotNull(sound.Native);        // escape hatch present
            Assert.False(sound.IsPlaying);       // a null sound is never playing
            sound.Play();                        // controls delegate without throwing
            sound.Stop();

            await sound.Finished;   // awaits the observable (already-stopped → completes immediately)

            probe.FinishedAwaited = true;
            life.StopApplication();
        }
    }

    [Fact]
    public void Audio3DAttachmentsUpdateFromAudioLoop()
    {
        var probe = new Probe();
        var builder = NewBuilder(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        builder.Build().Run();

        Assert.True(probe.AudioUpdates > 0, "audioLoop should have run");
        Assert.True(probe.SpatialUpdates > 0, "registered Audio3D scopes should update from audioLoop");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var audio = sp.GetRequiredService<AudioService>();
            var audio3d = Assert.IsType<Audio3D>(sp.GetRequiredService<IAudio3D>());
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var listener = scene.Root.AttachNewNode("listener");
            var emitter = scene.Root.AttachNewNode("emitter");
            var sound = audio.Sfx.GetNullSound();

            audio3d.AttachListener(listener);
            audio3d.Attach(sound, emitter);
            audio3d.SetVelocity(sound, new LVector3f(1f, 0f, 0f));
            audio3d.SetVelocityAuto(sound);
            audio3d.DistanceFactor = 2.0f;

            for (int i = 0; i < 5; i++)
            {
                emitter.SetX(i + 1);
                await PandaTask.NextFrame();
            }

            probe.AudioUpdates = audio.UpdateCount;
            probe.SpatialUpdates = audio3d.UpdateCount;

            audio3d.Detach(sound);
            audio3d.Dispose();
            life.StopApplication();
        }
    }
}
