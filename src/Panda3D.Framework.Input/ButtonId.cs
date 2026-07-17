using System;
using System.Text.Json.Serialization;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// A stable, type-safe handle to a physical button. Wraps Panda's runtime button-registry index — never
/// persist that; persist <see cref="Name"/> and reload via <see cref="FromName"/>. Obtain values from
/// <see cref="Keys"/>/<see cref="Mouse"/>/<see cref="Gamepad"/>.
/// </summary>
[JsonConverter(typeof(ButtonIdJsonConverter))]
public readonly struct ButtonId : IEquatable<ButtonId>
{
    readonly int _handle;

    internal ButtonId(int handle) => _handle = handle;

    /// <summary>The raw native button-registry index. Internal — an unstable handle, never persist it.</summary>
    internal int Handle => _handle;

    /// <summary>The unset button (index 0). Matches nothing.</summary>
    public static ButtonId None => default;

    /// <summary>True if this is the unset button.</summary>
    public bool IsNone => _handle == 0;

    /// <summary>The stable registry name (e.g. <c>"escape"</c>, <c>"w"</c>, <c>"mouse1"</c>) — persist this.</summary>
    public string Name => new ButtonHandle(_handle).GetName();

    /// <summary>Resolve a button from its stable registry name; an unknown/empty name resolves to <see cref="None"/>.</summary>
    public static ButtonId FromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return None;
        return new ButtonId(ButtonRegistry.Ptr().FindButton(name));
    }

    /// <inheritdoc/>
    public bool Equals(ButtonId other) => _handle == other._handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ButtonId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _handle;

    /// <summary>Handle-identity equality.</summary>
    public static bool operator ==(ButtonId left, ButtonId right) => left._handle == right._handle;

    /// <summary>Handle-identity inequality.</summary>
    public static bool operator !=(ButtonId left, ButtonId right) => left._handle != right._handle;

    /// <inheritdoc/>
    public override string ToString() => IsNone ? "<none>" : Name;
}
