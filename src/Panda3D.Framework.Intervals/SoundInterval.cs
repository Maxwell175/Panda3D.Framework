using System;
using Panda3D.Core;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Holds a timeline slot while a native sound plays. The interval seeks the sound to the timeline
/// time, so scrubbing or jumping the timeline keeps the sound aligned.
/// </summary>
public sealed class SoundInterval : ManagedInterval
{
    readonly AudioSound _sound;
    readonly bool _loop;
    readonly float _volume;
    readonly double _startTime;
    readonly NodePath? _emitter;

    public SoundInterval(
        AudioSound sound,
        bool loop = false,
        double duration = 0,
        float volume = 1f,
        double startTime = 0,
        NodePath? emitter = null)
        : base(ResolveDuration(sound, duration, startTime), name: "sound")
    {
        _sound = sound ?? throw new ArgumentNullException(nameof(sound));
        _loop = loop;
        _volume = volume;
        _startTime = Math.Max(0, startTime);
        _emitter = emitter;
    }

    public override void Initialize(double t)
    {
        _sound.SetLoop(_loop);
        _sound.SetVolume(_volume);
        Seek(t);
        _sound.Play();
    }

    public override void Step(double t) => Seek(t);

    public override void Complete() => _sound.Stop();

    public override void Interrupt() => _sound.Stop();

    void Seek(double t)
    {
        if (_emitter is not null)
        {
            var p = _emitter.GetNetTransform().GetPos();
            _sound.Set3dAttributes(p.GetX(), p.GetY(), p.GetZ(), 0f, 0f, 0f);
        }

        _sound.SetTime((float)Math.Max(0, _startTime + t));
    }

    static double ResolveDuration(AudioSound? sound, double duration, double startTime)
    {
        if (sound is null || duration > 0) return duration;
        return Math.Max(0, sound.Length() - Math.Max(0, startTime));
    }
}
