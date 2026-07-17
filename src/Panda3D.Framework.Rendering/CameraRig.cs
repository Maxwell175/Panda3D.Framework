using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>
/// Composed camera + lens ergonomics over a native <c>Camera</c> node. The camera node is a normal
/// scene node (parent/move it) that renders a chosen scene root.
/// </summary>
internal sealed class CameraRig : ICameraRig
{
    readonly Camera _camera;

    public CameraRig(NodePath parent, NodePath scene, string name, Lens lens)
    {
        _camera = new Camera(name, lens);
        _camera.SetScene(scene);
        Node = parent.AttachNewNode(_camera);
    }

    public NodePath Node { get; }

    public Lens Lens => _camera.GetLens();

    public void SetPerspective(float fov)
    {
        var lens = new PerspectiveLens();
        lens.SetFov(fov);
        // a fresh lens defaults to square (1:1); carry the aspect across the swap
        lens.SetAspectRatio(_camera.GetLens().GetAspectRatio());
        _camera.SetLens(lens);
    }

    public void SetOrthographic(float filmW, float filmH)
    {
        var lens = new OrthographicLens();
        lens.SetFilmSize(filmW, filmH);
        lens.SetAspectRatio(_camera.GetLens().GetAspectRatio());
        _camera.SetLens(lens);
    }
}
