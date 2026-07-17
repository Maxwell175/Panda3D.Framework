using Panda3D.Core;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Input;

/// <summary>Turns a physical button handle into the user's keyboard legend (layout-aware) for HUD / rebind UI.</summary>
public interface IButtonLabels
{
    /// <summary>The user's legend for a physical button (e.g. "Z" on AZERTY for the physical-W key).</summary>
    string Label(ButtonId button);
}

internal sealed class ButtonLabels : IButtonLabels
{
    readonly IViewManager _views;

    public ButtonLabels(IViewManager views) => _views = views;

    public string Label(ButtonId button)
    {
        var window = _views.MainOrNull?.Window;
        if (window is not null)
        {
            var mapped = window.GetKeyboardMap().GetMappedButtonLabel(button.Handle);
            if (!string.IsNullOrEmpty(mapped)) return mapped;
        }
        return button.Name;
    }
}
