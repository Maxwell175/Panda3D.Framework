using System;
using Panda3D.Core;

namespace Panda3D.Framework.Gui;

public sealed class Label : IDisposable
{
    bool _disposed;

    public Label(string text, string name = "label")
    {
        TextNode = new TextNode(name);
        TextNode.SetText(text);
        Node = new NodePath(TextNode);
    }

    public NodePath Node { get; }
    public TextNode TextNode { get; }

    public string Text
    {
        get => TextNode.GetText();
        set => TextNode.SetText(value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!Node.IsEmpty()) Node.RemoveNode();
    }
}
