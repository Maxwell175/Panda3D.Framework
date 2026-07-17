using System.ComponentModel;

namespace Panda3D.Framework;

/// <summary>
/// Marker for a service that already advances the global clock once per frame (e.g. Rendering's
/// <c>RenderFrame</c>). When one is present, <c>AddClock</c> won't also tick, avoiding a double-advance.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IClockTickSource
{
}
