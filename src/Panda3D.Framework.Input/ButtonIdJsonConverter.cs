using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Panda3D.Framework.Input;

/// <summary>Serializes a <see cref="ButtonId"/> as its stable registry <see cref="ButtonId.Name"/>.</summary>
public sealed class ButtonIdJsonConverter : JsonConverter<ButtonId>
{
    /// <inheritdoc/>
    public override ButtonId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ButtonId.FromName(reader.GetString() ?? string.Empty);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ButtonId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.IsNone ? string.Empty : value.Name);
}
