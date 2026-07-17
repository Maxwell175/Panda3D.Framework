using System;
using PandaParticleSystemManager = Panda3D.Physics.ParticleSystemManager;
using PandaPhysicsManager = Panda3D.Physics.PhysicsManager;

namespace Panda3D.Framework.Particles;

/// <summary>Built-in Panda physics and particle managers, exposed explicitly and updated at <see cref="FrameSlots.Collision"/>.</summary>
public interface IParticles : IDisposable
{
    /// <summary>Create a framework particle effect rooted at a movable node.</summary>
    ParticleEffect Create(string name);

    /// <summary>Native built-in physics manager for forces and physical nodes.</summary>
    PandaPhysicsManager Physics { get; }

    /// <summary>Native particle system manager that owns the effect systems.</summary>
    PandaParticleSystemManager Manager { get; }
}
