using System;
using Panda3D.Core;
using Panda3D.Framework.Intervals;

namespace Panda3D.Framework.Particles;

/// <summary>
/// Holds a timeline slot while a particle effect emits, with optional soft-stop lead time and
/// cleanup at the end of the slot.
/// </summary>
public sealed class ParticleInterval : ManagedInterval
{
    readonly ParticleEffect _effect;
    readonly NodePath _parent;
    readonly double _softStopT;
    readonly bool _cleanup;
    bool _softStopped;
    bool _disposedEffect;

    public ParticleInterval(
        ParticleEffect effect,
        NodePath parent,
        double duration,
        double softStopT = 0,
        bool cleanup = false)
        : base(Math.Max(0, duration), name: "particle")
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _softStopT = Math.Max(0, softStopT);
        _cleanup = cleanup;
    }

    public override void Initialize(double t)
    {
        _effect.Node.ReparentTo(_parent);
        _effect.SoftStart();
        _softStopped = false;
        _disposedEffect = false;
        Step(t);
    }

    public override void Step(double t)
    {
        if (!_softStopped && _softStopT > 0 && t >= Math.Max(0, Duration - _softStopT))
            SoftStop();
    }

    public override void Complete() => Finish();

    public override void Interrupt() => Finish();

    void Finish()
    {
        SoftStop();
        if (_cleanup && !_disposedEffect)
        {
            _disposedEffect = true;
            _effect.Dispose();
        }
    }

    void SoftStop()
    {
        if (_softStopped) return;
        _effect.SoftStop();
        _softStopped = true;
    }
}
