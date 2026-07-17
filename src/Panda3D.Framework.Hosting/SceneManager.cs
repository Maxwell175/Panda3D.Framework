using System;
using System.Collections.Concurrent;
using Panda3D.Core;

namespace Panda3D.Framework.Hosting;

/// <summary>
/// Trivial <see cref="ISceneManager"/>: a default <c>render</c> root plus a name→root registry of
/// get-or-create independent 3-D roots.
/// </summary>
internal sealed class SceneManager : ISceneManager
{
    readonly NodePath _root = new NodePath("render");
    readonly ConcurrentDictionary<string, NodePath> _named = new();

    public NodePath Root => _root;

    public NodePath GetRoot(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _named.GetOrAdd(name, static n => new NodePath(n));
    }
}
