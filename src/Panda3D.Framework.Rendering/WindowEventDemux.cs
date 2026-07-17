namespace Panda3D.Framework.Rendering;

/// <summary>
/// Panda raises a single <c>window-event</c> per window for ANY property change. This diffs the current
/// <c>WindowProperties</c> snapshot against the previous one to recover the four typed transitions.
/// </summary>
internal readonly record struct WindowSnapshot(bool Open, bool Foreground, bool Minimized, bool HasSize, int Width, int Height);

/// <summary>What changed between two snapshots.</summary>
internal readonly record struct WindowChanges(
    bool Closed,
    bool Resized,
    bool FocusChanged, bool Foreground,
    bool MinimizedChanged, bool Minimized);

internal static class WindowEventDemux
{
    public static WindowChanges Diff(WindowSnapshot prev, WindowSnapshot curr)
    {
        bool closed = prev.Open && !curr.Open;

        bool resized = curr.HasSize && (!prev.HasSize || prev.Width != curr.Width || prev.Height != curr.Height);

        bool focusChanged = prev.Foreground != curr.Foreground;
        bool minimizedChanged = prev.Minimized != curr.Minimized;

        return new WindowChanges(
            closed,
            resized,
            focusChanged, curr.Foreground,
            minimizedChanged, curr.Minimized);
    }
}
