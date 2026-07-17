using System;
using System.Collections.Generic;
using System.Threading;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Audio;

internal sealed class AudioService : IAudio, IDisposable
{
    static int _finishedCounter;

    readonly INamedEventBus _bus;
    readonly List<Audio3D> _spatial = new();
    int _disposed;

    public AudioService(INamedEventBus bus)
    {
        _bus = bus;
        Sfx = AudioManager.CreateAudioManager();
        Music = AudioManager.CreateAudioManager();
    }

    public AudioManager Sfx { get; }
    public AudioManager Music { get; }

    public ISound LoadSfx(string path, bool positional = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Wrap(Sfx.GetSound(Filename.FromOsSpecific(path), positional));
    }

    public ISound LoadMusic(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Wrap(Music.GetSound(Filename.FromOsSpecific(path), positional: false));
    }

    public ISound Wrap(AudioSound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        return new Sound(sound, WhenFinished);
    }

    // completes when the sound's native finished event fires; already-stopped completes immediately
    PandaTask WhenFinished(AudioSound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);

        string doneEvent = $"snd-done-{Interlocked.Increment(ref _finishedCounter)}";
        var tcs = new PandaTaskCompletionSource();

        IDisposable? sub = null;
        sub = _bus.Subscribe(doneEvent, _ =>
        {
            sub?.Dispose();
            tcs.TrySetResult();
        });

        sound.SetFinishedEvent(doneEvent);

        // already stopped: no future native completion to observe, so complete now
        if (sound.Status() != AudioSoundSoundStatus.Playing)
        {
            sub.Dispose();
            tcs.TrySetResult();
        }

        return tcs.Task;
    }

    internal IDisposable Register(Audio3D audio3d)
    {
        ArgumentNullException.ThrowIfNull(audio3d);
        _spatial.Add(audio3d);
        return new Registration(this, audio3d);
    }

    internal int UpdateCount { get; private set; }

    internal void Update(double dt)
    {
        foreach (var spatial in _spatial.ToArray())
            spatial.Update(dt);

        Sfx.Update();
        Music.Update();
        UpdateCount++;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _spatial.Clear();
        Sfx.Shutdown();
        Music.Shutdown();
    }

    sealed class Registration : IDisposable
    {
        readonly AudioService _owner;
        Audio3D? _audio3d;

        public Registration(AudioService owner, Audio3D audio3d)
        {
            _owner = owner;
            _audio3d = audio3d;
        }

        public void Dispose()
        {
            var audio3d = Interlocked.Exchange(ref _audio3d, null);
            if (audio3d is not null)
                _owner._spatial.Remove(audio3d);
        }
    }
}
