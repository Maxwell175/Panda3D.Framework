using System;
using Panda3D.Core;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Gui;

/// <summary>
/// Per-view GUI owner. Resolve it from <see cref="IView.Services"/> so widgets are attached to that
/// view's overlay roots.
/// </summary>
public interface IGui : IDisposable
{
    /// <summary>The view this GUI scope belongs to.</summary>
    IView View { get; }

    /// <summary>Parent a PGui widget under the view overlay, or under the supplied parent.</summary>
    T Add<T>(T widget, NodePath? parent = null) where T : Widget;

    /// <summary>Parent a text-only label under the view overlay, or under the supplied parent.</summary>
    Label Add(Label label, NodePath? parent = null);
}
