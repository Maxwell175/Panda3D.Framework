namespace Panda3D.Framework.Rendering;

/// <summary>Aspect-ratio math applied to the 2-D overlay and the 3-D lens on every resize.</summary>
internal static class Overlay2dMath
{
    public static float AspectRatio(int width, int height)
        => height <= 0 ? 1f : (float)width / height;

    /// <summary>
    /// The scale for the aspect-correct overlay root. Wide (ar ≥ 1): squash x by 1/ar so ±1 vertical
    /// stays square; tall (ar &lt; 1): squash y/z by ar.
    /// </summary>
    public static (float X, float Y, float Z) OverlayScale(float aspectRatio)
        => aspectRatio >= 1f ? (1f / aspectRatio, 1f, 1f) : (1f, aspectRatio, aspectRatio);

    /// <summary>Local overlay-space edge coordinates after aspect scaling.</summary>
    public static (float Left, float Right, float Bottom, float Top) OverlayEdges(float aspectRatio)
        => aspectRatio >= 1f
            ? (-aspectRatio, aspectRatio, -1f, 1f)
            : (-1f, 1f, -1f / aspectRatio, 1f / aspectRatio);

    /// <summary>The scale for the pixel-exact root so one unit equals one pixel.</summary>
    public static (float X, float Y, float Z) PixelScale(int width, int height)
        => (width <= 0 ? 1f : 2f / width, 1f, height <= 0 ? 1f : 2f / height);
}
