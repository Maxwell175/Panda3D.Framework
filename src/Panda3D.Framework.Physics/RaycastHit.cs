using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>
/// One ray/pick result. <see cref="Into"/> is the struck node; the surface geometry and
/// <see cref="Distance"/> are in the coordinate space of the traversed root.
/// </summary>
public readonly record struct RaycastHit(NodePath Into, LPoint3f SurfacePoint, LVector3f SurfaceNormal, float Distance);
