using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>Helpers for reducing a loaded model to just the collision geometry a simulation needs.</summary>
public static class CollisionModels
{
    /// <summary>
    /// Strips <paramref name="model"/> down to its <c>CollisionNode</c>s in place, keeping the hierarchy:
    /// every other node is replaced with a plain <c>PandaNode</c> (name, transform and children survive).
    /// Returns the same NodePath for chaining.
    /// </summary>
    public static NodePath StripToCollision(this NodePath model)
    {
        int collision = CollisionNode.GetClassType();
        int plain = PandaNode.GetClassType();
        var nodes = model.FindAllMatches("**");
        foreach (var path in nodes)
        {
            var node = path.Node();
            if (!node.IsOfType(collision) && !node.IsExactType(plain))
                new PandaNode(node.GetName()).ReplaceNode(node);
        }

        return model;
    }
}
