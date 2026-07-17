using System;
using Panda3D.Core;

namespace Panda3D.Framework.Audio;

/// <summary>
/// An attach registry that pushes node positions/velocities into the native 3-D audio primitives
/// every audio frame.
/// </summary>
public interface IAudio3D : IDisposable
{
    /// <summary>Attach the listener node, usually the active camera rig.</summary>
    void AttachListener(NodePath node);

    /// <summary>Attach a positional sound to an emitter node.</summary>
    void Attach(AudioSound sound, NodePath emitter);

    /// <summary>Stop tracking a positional sound.</summary>
    void Detach(AudioSound sound);

    /// <summary>Use an explicit velocity for doppler calculations.</summary>
    void SetVelocity(AudioSound sound, LVector3f velocity);

    /// <summary>Derive velocity from the emitter's frame-over-frame world position delta.</summary>
    void SetVelocityAuto(AudioSound sound);

    /// <summary>Audio units per meter, delegated to the native manager.</summary>
    float DistanceFactor { get; set; }
}
