namespace Panda3D.Framework.Rendering;

/// <summary>Options for opening a view.</summary>
public sealed class ViewOptions
{
    /// <summary>Named scene root to view; null = <see cref="ISceneManager.Root"/>.</summary>
    public string? SceneRoot { get; set; }

    /// <summary>Window creation options.</summary>
    public WindowOptions Window { get; set; } = new();

    /// <summary>Create the aspect2d/pixel2d overlay (on by default).</summary>
    public bool Setup2d { get; set; } = true;

    /// <summary>Create an offscreen buffer instead of a window (headless / RTT / server rendering).</summary>
    public bool Offscreen { get; set; }
}
