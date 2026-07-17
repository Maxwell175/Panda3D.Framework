using System.Text.Json;
using Panda3D.Framework.Input;
using Xunit;

namespace Panda3D.Framework.Input.Tests;

public sealed class ButtonIdTests
{
    [Fact]
    public void SerializesByStableNameAndRoundTrips()
    {
        var w = Keys.Ascii('w');

        // A button has a stable registry name...
        string name = w.Name;
        Assert.False(string.IsNullOrEmpty(name));

        // ...and serializes to that name, not the volatile handle index.
        string json = JsonSerializer.Serialize(w);
        Assert.Equal($"\"{name}\"", json);

        // Round-trips back to the same button (a saved keymap reloads correctly).
        var restored = JsonSerializer.Deserialize<ButtonId>(json);
        Assert.Equal(w, restored);
        Assert.Equal(name, restored.Name);
    }

    [Fact]
    public void UnknownNameDegradesToNone()
    {
        var button = ButtonId.FromName("no-such-button-xyz");
        Assert.True(button.IsNone);
        Assert.NotEqual(Keys.Ascii('w'), button);
    }
}
