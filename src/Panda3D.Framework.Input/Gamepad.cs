using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>The gamepad button vocabulary as type-safe <see cref="ButtonId"/> values.</summary>
public static class Gamepad
{
    public static ButtonId A => new(GamepadButton.FaceA());
    public static ButtonId B => new(GamepadButton.FaceB());
    public static ButtonId X => new(GamepadButton.FaceX());
    public static ButtonId Y => new(GamepadButton.FaceY());

    public static ButtonId DpadUp => new(GamepadButton.DpadUp());
    public static ButtonId DpadDown => new(GamepadButton.DpadDown());
    public static ButtonId DpadLeft => new(GamepadButton.DpadLeft());
    public static ButtonId DpadRight => new(GamepadButton.DpadRight());

    public static ButtonId LeftShoulder => new(GamepadButton.Lshoulder());
    public static ButtonId RightShoulder => new(GamepadButton.Rshoulder());
    public static ButtonId LeftTrigger => new(GamepadButton.Ltrigger());
    public static ButtonId RightTrigger => new(GamepadButton.Rtrigger());
    public static ButtonId LeftStick => new(GamepadButton.Lstick());
    public static ButtonId RightStick => new(GamepadButton.Rstick());

    public static ButtonId Start => new(GamepadButton.Start());
    public static ButtonId Back => new(GamepadButton.Back());
    public static ButtonId Guide => new(GamepadButton.Guide());
}
