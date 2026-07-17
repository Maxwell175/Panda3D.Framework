namespace Panda3D.Framework.Rendering;

/// <summary>
/// The seeded per-view scope holder. Per-view services (<c>IGui</c>, <c>IInput</c>, …) take this in
/// their constructor, so resolving them from <c>view.Services</c> binds them to that view.
/// </summary>
public sealed class ViewContext
{
    /// <summary>The view this scope belongs to (seeded by <c>OpenView</c>).</summary>
    public IView? View { get; internal set; }
}
