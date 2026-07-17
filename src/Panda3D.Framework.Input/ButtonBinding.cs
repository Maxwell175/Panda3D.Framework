namespace Panda3D.Framework.Input;

/// <summary>A single physical button → bool (also 0/1 into a value action).</summary>
public sealed class ButtonBinding : IBinding
{
    public ButtonBinding(ButtonId button) => Button = button;
    public ButtonId Button { get; set; }
}
