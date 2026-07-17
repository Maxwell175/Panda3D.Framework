using System.Collections.Generic;

namespace Panda3D.Framework.Actors;

/// <summary>A part definition: a single model (or per-LOD models) plus named animations.</summary>
public sealed class ActorPartDef
{
    /// <summary>The part model, when the actor has no LODs.</summary>
    public string? Model { get; set; }

    /// <summary>Per-LOD model paths, keyed by LOD name.</summary>
    public IDictionary<string, string> ModelByLod { get; } = new Dictionary<string, string>();

    /// <summary>Named animation paths for this part.</summary>
    public IDictionary<string, string> Anims { get; } = new Dictionary<string, string>();
}
