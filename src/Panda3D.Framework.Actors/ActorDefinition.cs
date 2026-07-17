using System.Collections.Generic;

namespace Panda3D.Framework.Actors;

/// <summary>A full actor definition: LOD bands and named parts.</summary>
public sealed class ActorDefinition
{
    /// <summary>The LOD bands, if any.</summary>
    public IList<LodLevel> Lods { get; } = new List<LodLevel>();

    /// <summary>The parts, keyed by part name.</summary>
    public IDictionary<string, ActorPartDef> Parts { get; } = new Dictionary<string, ActorPartDef>();
}
