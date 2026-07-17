using System;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// Raw device polling for one view. Resolve it from a view's scope (<c>view.Input()</c> or
/// <c>view.Services</c>), not the root provider. Polling is physical (raw scan position).
/// </summary>
public interface IInput
{
    /// <summary>Whether the physical button is down (from <see cref="Keys"/>/<see cref="Mouse"/>/<see cref="Gamepad"/>).</summary>
    bool IsDown(ButtonId button);

    /// <summary>Down this frame, up last (edge).</summary>
    bool Pressed(ButtonId button);

    /// <summary>Up this frame, down last (edge).</summary>
    bool Released(ButtonId button);

    /// <summary>The pointer position over this view, or null when the pointer isn't over it.</summary>
    LPoint2f? MousePosition { get; }

    /// <summary>Whether the pointer is over a 2-D UI region (for gating gameplay clicks).</summary>
    bool IsOverUi { get; }

    /// <summary>The next physical button the user presses (for a rebind UI); null until one arrives.</summary>
    ButtonId? CaptureNext();
}
