using System;
using System.Reactive;
using System.Reactive.Subjects;
using Panda3D.Async;
using Panda3D.Core;

namespace Panda3D.Framework.Audio;

/// <summary>
/// Thin managed handle over a native <see cref="AudioSound"/>. The <see cref="Finished"/> observable is
/// built lazily, so a sound played but never awaited or observed arms no event.
/// </summary>
internal sealed class Sound : ISound
{
    readonly AudioSound _sound;
    readonly Func<AudioSound, PandaTask> _whenFinished;
    AsyncSubject<Unit>? _finished;

    public Sound(AudioSound sound, Func<AudioSound, PandaTask> whenFinished)
    {
        _sound = sound;
        _whenFinished = whenFinished;
    }

    public AudioSound Native => _sound;
    public bool IsPlaying => _sound.Status() == AudioSoundSoundStatus.Playing;

    public float Volume { get => _sound.GetVolume(); set => _sound.SetVolume(value); }
    public bool Loop { get => _sound.GetLoop(); set => _sound.SetLoop(value); }

    public void Play() => _sound.Play();
    public void Stop() => _sound.Stop();

    public IObservable<Unit> Finished
    {
        get
        {
            if (_finished is null)
            {
                _finished = new AsyncSubject<Unit>();
                Bridge(_finished);
            }
            return _finished;
        }
    }

    // relays the one-shot finished-task onto the replaying subject; the task never faults
    async void Bridge(AsyncSubject<Unit> subject)
    {
        await _whenFinished(_sound);
        subject.OnNext(Unit.Default);
        subject.OnCompleted();
    }
}
