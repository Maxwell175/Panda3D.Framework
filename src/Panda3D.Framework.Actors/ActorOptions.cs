namespace Panda3D.Framework.Actors;

/// <summary>Per-load actor options.</summary>
public sealed class ActorOptions
{
    /// <summary>Interpolate between animation frames.</summary>
    public bool FrameBlend { get; set; }

    /// <summary>Keep a loose part hierarchy rather than flattening.</summary>
    public bool LooseHierarchy { get; set; }
}
