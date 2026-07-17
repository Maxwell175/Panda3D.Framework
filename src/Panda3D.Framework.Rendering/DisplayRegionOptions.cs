using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>Options for an extra display region.</summary>
public sealed class DisplayRegionOptions
{
    /// <summary>Region rectangle in normalized [0,1] coordinates (left, right, bottom, top).</summary>
    public (float L, float R, float B, float T) Dimensions { get; set; } = (0, 1, 0, 1);

    /// <summary>Sort order among the output's regions.</summary>
    public int Sort { get; set; }

    /// <summary>Optional camera to bind; if null, the caller binds one later.</summary>
    public NodePath? Camera { get; set; }
}
