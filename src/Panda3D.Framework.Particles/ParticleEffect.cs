using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Panda3D.Core;
using Panda3D.Physics;
using PandaPhysicsManager = Panda3D.Physics.PhysicsManager;
using PandaParticleSystem = Panda3D.Physics.ParticleSystem;
using PandaParticleSystemManager = Panda3D.Physics.ParticleSystemManager;

namespace Panda3D.Framework.Particles;

/// <summary>A node plus a collection of native particle systems, with lifecycle operations projected over every system.</summary>
public sealed class ParticleEffect : IDisposable
{
    readonly PandaPhysicsManager _physics;
    readonly PandaParticleSystemManager _manager;
    readonly Dictionary<PandaParticleSystem, SystemNode> _nodes = new();
    int _disposed;

    internal ParticleEffect(string name, PandaPhysicsManager physics, PandaParticleSystemManager manager)
    {
        _physics = physics;
        _manager = manager;
        Node = new NodePath(name);
        Systems = new ParticleSystemList(this);
    }

    /// <summary>The effect root. Parent, position, and scale it like any other node.</summary>
    public NodePath Node { get; }

    /// <summary>Native particle systems. Configure factory, renderer, emitter, and flags through the bindings.</summary>
    public IList<PandaParticleSystem> Systems { get; }

    /// <summary>Start emission on every system.</summary>
    public void SoftStart()
    {
        ThrowIfDisposed();
        foreach (var system in Systems)
            system.SoftStart();
    }

    /// <summary>Stop emission on every system while letting live particles drain.</summary>
    public void SoftStop()
    {
        ThrowIfDisposed();
        foreach (var system in Systems)
            system.SoftStop();
    }

    void Attach(PandaParticleSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        var physical = new PhysicalNode($"{Node.GetName()}-system-{_nodes.Count}");
        physical.AddPhysical(system);
        var physicalNode = Node.AttachNewNode(physical);

        system.SetRenderParent(physical);
        _physics.AttachPhysical(system);
        _manager.AttachParticlesystem(system);
        _nodes[system] = new SystemNode(physical, physicalNode);
    }

    void Detach(PandaParticleSystem system)
    {
        _manager.RemoveParticlesystem(system);
        _physics.RemovePhysical(system);
        if (_nodes.Remove(system, out var node))
        {
            node.Physical.RemovePhysical(system);
            node.Node.RemoveNode();
        }
    }

    void ThrowIfDisposed()
    {
        if (_disposed != 0) throw new ObjectDisposedException(nameof(ParticleEffect));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Systems.Clear();
        Node.RemoveNode();
    }

    sealed class ParticleSystemList : Collection<PandaParticleSystem>
    {
        readonly ParticleEffect _effect;

        public ParticleSystemList(ParticleEffect effect) => _effect = effect;

        protected override void InsertItem(int index, PandaParticleSystem item)
        {
            _effect.ThrowIfDisposed();
            _effect.Attach(item);
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, PandaParticleSystem item)
        {
            _effect.ThrowIfDisposed();
            _effect.Detach(this[index]);
            _effect.Attach(item);
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            _effect.Detach(this[index]);
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
                _effect.Detach(item);
            base.ClearItems();
        }
    }

    readonly record struct SystemNode(PhysicalNode Physical, NodePath Node);
}
