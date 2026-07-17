using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// The engine-wide data graph: the <c>data</c> root and the traverser the <c>dataLoop</c> runs each
/// epoch. Per-view mouse/keyboard chains are attached under the root.
/// </summary>
internal sealed class DataGraph
{
    readonly DataGraphTraverser _traverser = new();

    public NodePath Root { get; } = new NodePath("data");

    public void Traverse() => _traverser.Traverse(Root.Node());
}
