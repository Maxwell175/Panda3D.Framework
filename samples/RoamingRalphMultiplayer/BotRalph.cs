using Panda3D.Core;
using Panda3D.Framework.Physics;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// A headless Ralph: a <see cref="RalphBody"/> on a plain node with no <see cref="RalphAvatar"/> — no model,
/// no textures, no rendering. It simulates movement/collision/terrain exactly like the player and reports the
/// same <see cref="PlayerState"/> (including which animation it <em>would</em> be playing), so rendered
/// clients animate it correctly while it stays a pure simulation.
/// </summary>
internal sealed class BotRalph
{
    const float StartHeight = 1.5f;

    readonly RalphBody _body;

    public BotRalph(ICollisionWorld collisions, NodePath sceneRoot, LPoint3f start)
    {
        var node = sceneRoot.AttachNewNode("bot");
        node.SetPos(start.GetX(), start.GetY(), start.GetZ() + StartHeight);
        _body = new RalphBody(node, collisions, sceneRoot);
    }

    public void Update(float turn, float forward, float dt) => _body.Update(turn, forward, dt);

    public void SnapToTerrain() => _body.SnapToTerrain();

    public PlayerState Snapshot(ushort id, string name) => _body.Snapshot(id, name);
}
