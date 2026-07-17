using System;
using System.Collections.Generic;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Gui;

internal sealed class GuiService : IGui
{
    readonly INamedEventBus _bus;
    readonly List<IDisposable> _owned = new();
    bool _disposed;

    public GuiService(ViewContext context, IViewManager views, INamedEventBus bus)
    {
        _bus = bus;
        View = context.View ?? views.Main
            ?? throw new InvalidOperationException("IGui requires an active view.");
    }

    public IView View { get; }

    public T Add<T>(T widget, NodePath? parent = null) where T : Widget
    {
        ArgumentNullException.ThrowIfNull(widget);
        ThrowIfDisposed();

        widget.Node.ReparentTo(ResolveParent(parent));
        widget.Attach(_bus);
        _owned.Add(widget);
        return widget;
    }

    public Label Add(Label label, NodePath? parent = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        ThrowIfDisposed();

        label.Node.ReparentTo(ResolveParent(parent));
        _owned.Add(label);
        return label;
    }

    NodePath ResolveParent(NodePath? parent)
    {
        if (parent is not null) return parent;
        return View.Overlay2d
            ?? throw new InvalidOperationException("The target view was opened without a 2-D overlay.");
    }

    void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GuiService));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = _owned.Count - 1; i >= 0; i--)
            _owned[i].Dispose();
        _owned.Clear();
    }
}
