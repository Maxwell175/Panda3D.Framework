using System.Collections.Generic;
using Panda3D.Core;

namespace Panda3D.Framework.Actors;

/// <summary>
/// Advanced rigging surface of an <see cref="IActor"/> (via <see cref="IActor.Rig"/>): raw part bundles
/// and subparts, joint exposure/control, and level-of-detail.
/// </summary>
public interface IActorRig
{
    /// <summary>Define a named subpart of a part from an include/exclude joint set.</summary>
    void MakeSubpart(string name, SubpartDef def, string parent = ActorDefaults.DefaultPart);

    /// <summary>The part bundle for a part — the raw native animation-control container.</summary>
    PartBundle Part(string part = ActorDefaults.DefaultPart);

    /// <summary>Expose a joint as a read-only node that follows the animation.</summary>
    NodePath ExposeJoint(string joint, string part = ActorDefaults.DefaultPart, bool local = false);

    /// <summary>Take control of a joint so it can be posed from code.</summary>
    NodePath ControlJoint(string joint, string part = ActorDefaults.DefaultPart);

    /// <summary>Freeze a joint to a fixed transform.</summary>
    void FreezeJoint(string joint, TransformState transform, string part = ActorDefaults.DefaultPart);

    /// <summary>Release a previously controlled/frozen joint back to the animation.</summary>
    void ReleaseJoint(string joint, string part = ActorDefaults.DefaultPart);

    /// <summary>The LOD bands, if the actor has any.</summary>
    IReadOnlyList<LodLevel> Lods { get; }

    /// <summary>The LOD node, when the actor has LODs.</summary>
    LODNode? LodNode { get; }

    /// <summary>Scale animation update rate by distance for cheaper far-away actors.</summary>
    void SetAnimRateLod(LPoint3f center, float far, float near, float delayFactor = 1f);
}
