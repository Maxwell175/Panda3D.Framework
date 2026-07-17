using System;
using Panda3D.Core;
using Panda3D.Framework.Physics;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// A third-person camera that orbits and trails the local Ralph, held within a distance band and kept above
/// the terrain by its own ground ray.
/// </summary>
internal sealed class FollowCamera
{
    const string TerrainNode = "terrain";
    const float OrbitSpeed = 20f, MinDistance = 5f, MaxDistance = 10f;
    const float TerrainOffset = 1.5f, MinRalphOffset = 2f, LookAtHeight = 2f;

    readonly NodePath _camera;
    readonly NodePath _target;
    readonly NodePath _floater;
    readonly NodePath _sceneRoot;
    readonly GroundRay _ground;

    public FollowCamera(ICameraRig rig, ICollisionWorld collisions, NodePath sceneRoot, LocalRalph ralph)
    {
        _camera = rig.Node;
        _target = ralph.Node;
        _sceneRoot = sceneRoot;

        rig.SetPerspective(60f);
        _floater = _target.AttachNewNode("floater");
        _floater.SetZ(LookAtHeight);

        _camera.SetPos(_target.GetX(), _target.GetY() + MaxDistance, MinRalphOffset);
        _camera.LookAt(_floater);
        _ground = GroundRay.Below(collisions, _camera, "camRay");
    }

    /// <summary>Orbit by <paramref name="orbit"/>, keep in the distance band and above the ground, then aim at Ralph.</summary>
    public void Update(float orbit, float dt)
    {
        if (orbit != 0f)
            _camera.SetX(_camera, orbit * OrbitSpeed * dt);
        ClampDistance();
        SnapAboveTerrain();
        _camera.LookAt(_floater);
    }

    void ClampDistance()
    {
        float dx = _target.GetX() - _camera.GetX(), dy = _target.GetY() - _camera.GetY();
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.001f)
            return;

        float correction = distance > MaxDistance ? distance - MaxDistance
                         : distance < MinDistance ? distance - MinDistance
                         : 0f;
        if (correction != 0f)
            _camera.SetPos(_camera.GetX() + dx / distance * correction,
                           _camera.GetY() + dy / distance * correction,
                           _camera.GetZ());
    }

    void SnapAboveTerrain()
    {
        if (_ground.SurfaceZ(_sceneRoot, TerrainNode) is { } z)
            _camera.SetZ(z + TerrainOffset);

        float minZ = _target.GetZ() + MinRalphOffset;
        if (_camera.GetZ() < minZ)
            _camera.SetZ(minZ);
    }
}
