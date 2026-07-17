namespace Panda3D.Framework.Rendering;

/// <summary>
/// A named position on the aspect-correct 2-D overlay, kept pinned to that edge/corner as the window
/// resizes. Parent a widget to <see cref="IView.OverlayAnchors"/>[anchor] to pin it there.
/// </summary>
public enum OverlayAnchor
{
    Center,
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
