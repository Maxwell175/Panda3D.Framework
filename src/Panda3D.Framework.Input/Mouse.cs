using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>The mouse button vocabulary as type-safe <see cref="ButtonId"/> values.</summary>
public static class Mouse
{
    /// <summary>The button for a 1-based mouse button number.</summary>
    public static ButtonId Button(int number) => new(MouseButton.Button(number));

    public static ButtonId Left => new(MouseButton.One());
    public static ButtonId Middle => new(MouseButton.Two());
    public static ButtonId Right => new(MouseButton.Three());
    public static ButtonId Four => new(MouseButton.Four());
    public static ButtonId Five => new(MouseButton.Five());

    public static ButtonId WheelUp => new(MouseButton.WheelUp());
    public static ButtonId WheelDown => new(MouseButton.WheelDown());
    public static ButtonId WheelLeft => new(MouseButton.WheelLeft());
    public static ButtonId WheelRight => new(MouseButton.WheelRight());
}
