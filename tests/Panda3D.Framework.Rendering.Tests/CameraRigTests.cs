using Panda3D.Core;
using Panda3D.Framework.Rendering;
using Xunit;

namespace Panda3D.Framework.Rendering.Tests;

/// <summary>
/// Regression: switching the lens (perspective↔orthographic / changing FOV) must not drop the view's
/// aspect ratio. A fresh native lens defaults to square (1:1), so before the fix a
/// <c>SetPerspective</c> after view setup left the scene squished until the next window resize
/// re-applied the aspect.
/// </summary>
public sealed class CameraRigTests
{
    [Fact]
    public void SwappingLensPreservesAspectRatio()
    {
        const float aspect = 1280f / 720f;   // what a 720p window's ApplyAspect sets
        var rig = new CameraRig(new NodePath("parent"), new NodePath("scene"), "cam", new PerspectiveLens());
        rig.Lens.SetAspectRatio(aspect);

        rig.SetPerspective(60f);
        Assert.Equal(aspect, rig.Lens.GetAspectRatio(), 3);   // was reset to 1.0 before the fix

        rig.SetOrthographic(2f, 2f);
        Assert.Equal(aspect, rig.Lens.GetAspectRatio(), 3);
    }
}
