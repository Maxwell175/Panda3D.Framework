using Panda3D.Core;
using Panda3D.Framework.Physics;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// The simulation half of a Ralph: a transform node with a wall-slide collision body and a ground ray,
/// advanced by movement input into a position + heading + animation <em>state</em> (name/rate — it never
/// plays anything). The rendered player pairs this with a <see cref="RalphAvatar"/>; the headless
/// <see cref="BotRalph"/> uses it on its own. Either way it produces the same <see cref="Snapshot"/>.
/// </summary>
internal sealed class RalphBody
{
    const string TerrainNode = "terrain";
    const string RunAnim = "run", WalkAnim = "walk", IdleAnim = "idle";
    const float TurnSpeed = 300f, ForwardSpeed = 20f, BackwardSpeed = 10f;

    readonly NodePath _node;
    readonly NodePath _sceneRoot;
    readonly GroundRay _ground;

    public RalphBody(NodePath node, ICollisionWorld collisions, NodePath sceneRoot)
    {
        _node = node;
        _sceneRoot = sceneRoot;

        var body = new CollisionNode("ralph");
        body.SetFromCollideMask(BitMask32.Bit(0));
        body.SetIntoCollideMask(BitMask32.AllOff());
        body.AddSolid(new CollisionSphere(0f, 0f, 2f, 1.5f));
        body.AddSolid(new CollisionSphere(0f, -0.25f, 4f, 1.5f));
        collisions.AddPusher(_node.AttachNewNode(body), _node).Horizontal = true;

        _ground = GroundRay.Below(collisions, _node, "ralphRay");
    }

    public NodePath Node => _node;
    public string Anim { get; private set; } = IdleAnim;
    public float Rate { get; private set; } = 1f;

    /// <summary>Turn/move by this frame's input and pick the matching animation state.</summary>
    public void Update(float turn, float forward, float dt)
    {
        if (turn != 0f)
            _node.SetH(_node.GetH() + turn * TurnSpeed * dt);
        if (forward > 0f)
            _node.SetY(_node, -ForwardSpeed * forward * dt);
        else if (forward < 0f)
            _node.SetY(_node, BackwardSpeed * -forward * dt);

        if (forward > 0f) SetAnim(RunAnim, 1f);
        else if (forward < 0f) SetAnim(WalkAnim, -1f);
        else if (turn != 0f) SetAnim(WalkAnim, 1f);
        else SetAnim(IdleAnim, 1f);
    }

    void SetAnim(string anim, float rate)
    {
        Anim = anim;
        Rate = rate;
    }

    /// <summary>Snap to the terrain surface — call after the world's per-frame traverse fills the ground ray.</summary>
    public void SnapToTerrain()
    {
        if (_ground.SurfaceZ(_sceneRoot, TerrainNode) is { } z)
            _node.SetZ(z);
    }

    public PlayerState Snapshot(ushort id, string name)
        => new(id, name, _node.GetX(), _node.GetY(), _node.GetZ(), _node.GetH(), Anim, Rate);
}
