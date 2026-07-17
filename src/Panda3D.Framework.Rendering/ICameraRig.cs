using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>Composed lens/camera ergonomics over a native <c>Camera</c>.</summary>
public interface ICameraRig
{
    /// <summary>The camera node — parent/move it like any node.</summary>
    NodePath Node { get; }

    /// <summary>The lens, used directly.</summary>
    Lens Lens { get; }

    /// <summary>Replace the lens with a perspective lens of the given horizontal field of view (degrees).</summary>
    void SetPerspective(float fov);

    /// <summary>Replace the lens with an orthographic lens of the given film size.</summary>
    void SetOrthographic(float filmW, float filmH);
}
