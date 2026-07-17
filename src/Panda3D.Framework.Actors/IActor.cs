using System;
using System.Collections.Generic;
using Panda3D.Core;

namespace Panda3D.Framework.Actors;

/// <summary>An animated model: its node, animation controls, and (advanced) joint/subpart/LOD rigging.</summary>
public interface IActor : IDisposable
{
    /// <summary>The actor's scene-graph node.</summary>
    NodePath Node { get; }

    /// <summary>The underlying character.</summary>
    Character Character { get; }

    /// <summary>The animation control for a named clip on a part. Throws if the clip/part is unknown.</summary>
    IAnimControl Anim(string anim, string part = ActorDefaults.DefaultPart);

    /// <summary>
    /// Try to get the animation control for a named clip on a part; <see langword="false"/> (with
    /// <paramref name="control"/> null) if the clip or part is unknown.
    /// </summary>
    bool TryAnim(string anim, out IAnimControl? control, string part = ActorDefaults.DefaultPart);

    /// <summary>The names of the actor's animations.</summary>
    IReadOnlyCollection<string> Anims { get; }

    /// <summary>Enable animation blending on a part.</summary>
    void EnableBlend(string part = ActorDefaults.DefaultPart);

    /// <summary>Disable animation blending on a part.</summary>
    void DisableBlend(string part = ActorDefaults.DefaultPart);

    /// <summary>Set a blend weight for an animation on a part.</summary>
    void SetBlendWeight(string anim, float weight, string part = ActorDefaults.DefaultPart);

    /// <summary>Advanced rigging — raw part bundles/subparts, joint control, and LOD. See <see cref="IActorRig"/>.</summary>
    IActorRig Rig { get; }
}
