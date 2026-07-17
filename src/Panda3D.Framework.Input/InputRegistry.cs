using System.Collections.Generic;

namespace Panda3D.Framework.Input;

/// <summary>
/// Tracks the live <see cref="ViewInput"/>s so the <c>dataLoop</c> can roll their per-frame edge
/// snapshots before the traversal updates the watchers with new OS events.
/// </summary>
internal sealed class InputRegistry
{
    readonly List<ViewInput> _inputs = new();

    public void Register(ViewInput input) => _inputs.Add(input);
    public void Unregister(ViewInput input) => _inputs.Remove(input);

    public void CaptureAll()
    {
        foreach (var input in _inputs)
            input.CapturePreviousFrame();
    }
}
