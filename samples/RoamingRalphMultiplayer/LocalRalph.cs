using System;
using Panda3D.Core;
using Panda3D.Framework.Actors;
using Panda3D.Framework.Physics;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// The player's Ralph: a <see cref="RalphBody"/> (movement, collision, terrain) driving a
/// <see cref="RalphAvatar"/> (model + animation). <see cref="Snapshot"/> is what gets sent to other players.
/// </summary>
internal sealed class LocalRalph : IDisposable
{
    const float StartHeight = 1.5f;

    readonly RalphAvatar _avatar;
    readonly RalphBody _body;

    public LocalRalph(IActorLoader actors, ICollisionWorld collisions, NodePath sceneRoot, string modelRoot, LPoint3f start)
    {
        _avatar = new RalphAvatar(actors, sceneRoot, modelRoot);
        _avatar.Node.SetPos(start.GetX(), start.GetY(), start.GetZ() + StartHeight);
        _body = new RalphBody(_avatar.Node, collisions, sceneRoot);
    }

    public NodePath Node => _avatar.Node;

    public void Update(float turn, float forward, float dt)
    {
        _body.Update(turn, forward, dt);
        _avatar.PlayAnim(_body.Anim, _body.Rate);
    }

    public void SnapToTerrain() => _body.SnapToTerrain();

    public PlayerState Snapshot(ushort id, string name) => _body.Snapshot(id, name);

    public void Dispose() => _avatar.Dispose();
}
