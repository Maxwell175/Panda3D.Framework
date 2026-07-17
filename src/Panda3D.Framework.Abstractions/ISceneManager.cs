using Panda3D.Core;

namespace Panda3D.Framework;

/// <summary>
/// The 3-D world roots: a shared default <c>render</c> root plus named independent roots.
/// </summary>
public interface ISceneManager
{
    /// <summary>The default 3-D world root (<c>render</c>).</summary>
    NodePath Root { get; }

    /// <summary>A named, independent 3-D root; get-or-create (idempotent).</summary>
    NodePath GetRoot(string name);
}
