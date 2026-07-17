using Panda3D.Core;
using Xunit;

namespace Panda3D.Framework.Physics.Tests;

/// <summary>
/// <see cref="CollisionModels.StripToCollision"/> is a pure scene-graph transform (no host or graphics
/// pipe): every non-collision node becomes a plain <c>PandaNode</c>, while names, hierarchy and the
/// collision nodes themselves are preserved.
/// </summary>
public sealed class CollisionModelsTests
{
    [Fact]
    public void StripToCollisionDropsVisualsButKeepsCollisionAndHierarchy()
    {
        var root = new NodePath("root");
        var mid = root.AttachNewNode("mid");                        // plain marker — survives as-is
        var geom = mid.AttachNewNode(new GeomNode("geom"));         // visual — must become plain
        geom.AttachNewNode("under-geom");                           // child under the visual — hierarchy must survive
        mid.AttachNewNode(new CollisionNode("coll"));               // collision — must survive

        var returned = root.StripToCollision();

        Assert.Equal(root.Node(), returned.Node());                 // returns the same NodePath for chaining

        // Collision node preserved (still a CollisionNode).
        var coll = root.Find("**/coll");
        Assert.False(coll.IsEmpty());
        Assert.True(coll.Node().IsOfType(CollisionNode.GetClassType()));

        // The GeomNode is now a plain PandaNode: name kept, geometry type gone.
        var strippedGeom = root.Find("**/geom");
        Assert.False(strippedGeom.IsEmpty());
        Assert.True(strippedGeom.Node().IsExactType(PandaNode.GetClassType()));
        Assert.False(strippedGeom.Node().IsOfType(GeomNode.GetClassType()));

        // Hierarchy preserved: the child under the (now-plain) geom node is still reachable.
        Assert.False(root.Find("**/under-geom").IsEmpty());

        // Plain marker node untouched.
        Assert.False(root.Find("**/mid").IsEmpty());
    }
}
