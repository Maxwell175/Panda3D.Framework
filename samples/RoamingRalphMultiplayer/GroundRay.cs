using Panda3D.Core;
using Panda3D.Framework.Physics;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// A downward ray attached under a node and registered on the collision world's shared per-frame traverse.
/// Poll <see cref="SurfaceZ"/> after the traverse to read the terrain height below the node. Used by both
/// the player (foot placement) and the follow camera (staying above the ground).
/// </summary>
internal sealed class GroundRay
{
    const float CastHeight = 9f;   // start the ray well above the terrain and cast straight down

    readonly ICollisionQuery _query;

    GroundRay(ICollisionQuery query) => _query = query;

    public static GroundRay Below(ICollisionWorld collisions, NodePath node, string name)
    {
        var ray = new CollisionRay();
        ray.SetOrigin(0f, 0f, CastHeight);
        ray.SetDirection(0f, 0f, -1f);

        var collider = new CollisionNode(name);
        collider.SetFromCollideMask(BitMask32.Bit(0));   // hits terrain-masked into-geometry
        collider.SetIntoCollideMask(BitMask32.AllOff());
        collider.AddSolid(ray);
        return new GroundRay(collisions.AddQuery(node.AttachNewNode(collider)));
    }

    /// <summary>Terrain surface height (in <paramref name="space"/>) below the node, or null for no hit this frame.</summary>
    public float? SurfaceZ(NodePath space, string terrainName)
        => _query.NearestInto(terrainName) is { } hit ? hit.GetSurfacePoint(space).GetZ() : null;
}
