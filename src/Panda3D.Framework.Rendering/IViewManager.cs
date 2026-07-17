using System.Collections.Generic;

namespace Panda3D.Framework.Rendering;

/// <summary>Engine-wide view registry; runtime open/close.</summary>
public interface IViewManager
{
    /// <summary>Create a view + its per-output scope.</summary>
    IView OpenView(ViewOptions options);

    /// <summary>Dispose a view's scope deterministically.</summary>
    void CloseView(IView view);

    /// <summary>All open views.</summary>
    IReadOnlyList<IView> Views { get; }

    /// <summary>
    /// The default view (first-opened). Throws <see cref="System.InvalidOperationException"/> if none has
    /// been opened yet; use <see cref="MainOrNull"/> otherwise.
    /// </summary>
    IView Main { get; }

    /// <summary>The default view, or <see langword="null"/> if none has been opened yet.</summary>
    IView? MainOrNull { get; }
}
