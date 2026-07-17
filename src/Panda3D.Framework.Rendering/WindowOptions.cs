namespace Panda3D.Framework.Rendering;

/// <summary>
/// Window creation options, configured via <c>AddWindow</c>/<see cref="ViewOptions"/>.
/// </summary>
public sealed class WindowOptions
{
    /// <summary>Client size in pixels.</summary>
    public (int W, int H) Size { get; set; } = (1280, 720);

    /// <summary>Window title.</summary>
    public string Title { get; set; } = "Game";
}
