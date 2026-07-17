using System;
using System.Threading;
using PandaPhysicsManager = Panda3D.Physics.PhysicsManager;
using PandaParticleSystemManager = Panda3D.Physics.ParticleSystemManager;

namespace Panda3D.Framework.Particles;

internal sealed class ParticlesService : IParticles
{
    int _disposed;

    public ParticlesService()
    {
        Physics = new PandaPhysicsManager();
        Manager = new PandaParticleSystemManager();
    }

    public Panda3D.Physics.PhysicsManager Physics { get; }
    public Panda3D.Physics.ParticleSystemManager Manager { get; }

    internal int UpdateCount { get; private set; }

    public ParticleEffect Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        return new ParticleEffect(name, Physics, Manager);
    }

    internal void Update(float dt)
    {
        if (_disposed != 0) return;

        Physics.DoPhysics(dt);
        Manager.DoParticles(dt);
        UpdateCount++;
    }

    void ThrowIfDisposed()
    {
        if (_disposed != 0) throw new ObjectDisposedException(nameof(ParticlesService));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Manager.Clear();
        Physics.ClearPhysicals();
        Physics.ClearLinearForces();
        Physics.ClearAngularForces();

        if (Manager is IDisposable managerDisposable)
            managerDisposable.Dispose();
        if (Physics is IDisposable physicsDisposable)
            physicsDisposable.Dispose();
    }
}
