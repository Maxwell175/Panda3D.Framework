using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// The keyboard button vocabulary as type-safe <see cref="ButtonId"/> values. Printable characters go
/// through <see cref="Ascii"/> (e.g. <c>Keys.Ascii('w')</c>); named non-printable keys are properties.
/// </summary>
public static class Keys
{
    /// <summary>The button for a printable ASCII character, e.g. <c>Keys.Ascii('w')</c>.</summary>
    public static ButtonId Ascii(char c) => new(KeyboardButton.AsciiKey((byte)c));

    /// <summary>The button for a decimal digit 0–9.</summary>
    public static ButtonId Digit(int d) => Ascii((char)('0' + d));

    /// <summary>The button for a letter A–Z (case-insensitive).</summary>
    public static ButtonId Letter(char letter) => Ascii(char.ToLowerInvariant(letter));

    public static ButtonId Space => new(KeyboardButton.Space());
    public static ButtonId Enter => new(KeyboardButton.Enter());
    public static ButtonId Escape => new(KeyboardButton.Escape());
    public static ButtonId Tab => new(KeyboardButton.Tab());
    public static ButtonId Backspace => new(KeyboardButton.Backspace());
    public static ButtonId Delete => new(KeyboardButton.Del());
    public static ButtonId Insert => new(KeyboardButton.Insert());
    public static ButtonId Home => new(KeyboardButton.Home());
    public static ButtonId End => new(KeyboardButton.End());
    public static ButtonId PageUp => new(KeyboardButton.PageUp());
    public static ButtonId PageDown => new(KeyboardButton.PageDown());

    public static ButtonId Up => new(KeyboardButton.Up());
    public static ButtonId Down => new(KeyboardButton.Down());
    public static ButtonId Left => new(KeyboardButton.Left());
    public static ButtonId Right => new(KeyboardButton.Right());

    public static ButtonId Shift => new(KeyboardButton.Shift());
    public static ButtonId Control => new(KeyboardButton.Control());
    public static ButtonId Alt => new(KeyboardButton.Alt());
    public static ButtonId Meta => new(KeyboardButton.Meta());
    public static ButtonId LShift => new(KeyboardButton.Lshift());
    public static ButtonId RShift => new(KeyboardButton.Rshift());
    public static ButtonId LControl => new(KeyboardButton.Lcontrol());
    public static ButtonId RControl => new(KeyboardButton.Rcontrol());
    public static ButtonId LAlt => new(KeyboardButton.Lalt());
    public static ButtonId RAlt => new(KeyboardButton.Ralt());
    public static ButtonId CapsLock => new(KeyboardButton.CapsLock());

    public static ButtonId F1 => new(KeyboardButton.F1());
    public static ButtonId F2 => new(KeyboardButton.F2());
    public static ButtonId F3 => new(KeyboardButton.F3());
    public static ButtonId F4 => new(KeyboardButton.F4());
    public static ButtonId F5 => new(KeyboardButton.F5());
    public static ButtonId F6 => new(KeyboardButton.F6());
    public static ButtonId F7 => new(KeyboardButton.F7());
    public static ButtonId F8 => new(KeyboardButton.F8());
    public static ButtonId F9 => new(KeyboardButton.F9());
    public static ButtonId F10 => new(KeyboardButton.F10());
    public static ButtonId F11 => new(KeyboardButton.F11());
    public static ButtonId F12 => new(KeyboardButton.F12());
}
