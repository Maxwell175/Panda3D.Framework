using System;
using System.Collections.Generic;
using System.Threading;
using Panda3D.Core;

namespace Panda3D.Framework.Audio;

internal sealed class Audio3D : IAudio3D
{
    readonly AudioManager _manager;
    readonly IDisposable _registration;
    readonly Dictionary<AudioSound, Attachment> _attachments = new();
    NodePath? _listener;
    LPoint3f? _lastListenerPos;
    int _disposed;

    public Audio3D(AudioService audio)
    {
        _manager = audio.Sfx;
        _registration = audio.Register(this);
    }

    public void AttachListener(NodePath node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ThrowIfDisposed();
        _listener = node;
        _lastListenerPos = WorldPos(node);
    }

    public void Attach(AudioSound sound, NodePath emitter)
    {
        ArgumentNullException.ThrowIfNull(sound);
        ArgumentNullException.ThrowIfNull(emitter);
        ThrowIfDisposed();

        _attachments[sound] = new Attachment(sound, emitter, WorldPos(emitter));
    }

    public void Detach(AudioSound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        _attachments.Remove(sound);
    }

    public void SetVelocity(AudioSound sound, LVector3f velocity)
    {
        ArgumentNullException.ThrowIfNull(sound);
        ArgumentNullException.ThrowIfNull(velocity);
        ThrowIfDisposed();

        var attachment = RequireAttachment(sound);
        attachment.AutoVelocity = false;
        attachment.Velocity = new LVector3f(velocity);
    }

    public void SetVelocityAuto(AudioSound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        ThrowIfDisposed();
        RequireAttachment(sound).AutoVelocity = true;
    }

    public float DistanceFactor
    {
        get => _manager.Audio3dGetDistanceFactor();
        set => _manager.Audio3dSetDistanceFactor(value);
    }

    internal int UpdateCount { get; private set; }

    internal void Update(double dt)
    {
        if (_disposed != 0) return;

        UpdateListener(dt);
        UpdateEmitters(dt);
        UpdateCount++;
    }

    void UpdateListener(double dt)
    {
        if (_listener is null || _listener.IsEmpty()) return;

        var transform = _listener.GetNetTransform();
        var pos = transform.GetPos();
        var quat = transform.GetQuat();
        var vel = _lastListenerPos is null ? Zero() : VelocityBetween(_lastListenerPos, pos, dt);
        var forward = quat.GetForward();
        var up = quat.GetUp();

        _manager.Audio3dSetListenerAttributes(
            pos.X, pos.Y, pos.Z,
            vel.X, vel.Y, vel.Z,
            forward.X, forward.Y, forward.Z,
            up.X, up.Y, up.Z);

        _lastListenerPos = pos;
    }

    void UpdateEmitters(double dt)
    {
        foreach (var attachment in _attachments.Values)
        {
            if (attachment.Emitter.IsEmpty()) continue;

            var pos = WorldPos(attachment.Emitter);
            var vel = attachment.AutoVelocity
                ? VelocityBetween(attachment.LastPos, pos, dt)
                : attachment.Velocity;

            attachment.Sound.Set3dAttributes(pos.X, pos.Y, pos.Z, vel.X, vel.Y, vel.Z);
            attachment.LastPos = pos;
        }
    }

    Attachment RequireAttachment(AudioSound sound)
    {
        if (_attachments.TryGetValue(sound, out var attachment))
            return attachment;

        throw new InvalidOperationException("Sound is not attached to this Audio3D manager.");
    }

    static LPoint3f WorldPos(NodePath node) => node.GetNetTransform().GetPos();

    static LVector3f VelocityBetween(LVecBase3f previous, LVecBase3f current, double dt)
    {
        if (dt <= 0) return Zero();
        float invDt = (float)(1.0 / dt);
        return new LVector3f(
            (current.X - previous.X) * invDt,
            (current.Y - previous.Y) * invDt,
            (current.Z - previous.Z) * invDt);
    }

    static LVector3f Zero() => new(0f, 0f, 0f);

    void ThrowIfDisposed()
    {
        if (_disposed != 0) throw new ObjectDisposedException(nameof(Audio3D));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _attachments.Clear();
        _listener = null;
        _registration.Dispose();
    }

    sealed class Attachment
    {
        public Attachment(AudioSound sound, NodePath emitter, LPoint3f lastPos)
        {
            Sound = sound;
            Emitter = emitter;
            LastPos = lastPos;
        }

        public AudioSound Sound { get; }
        public NodePath Emitter { get; }
        public LPoint3f LastPos { get; set; }
        public LVector3f Velocity { get; set; } = Zero();
        public bool AutoVelocity { get; set; }
    }
}
