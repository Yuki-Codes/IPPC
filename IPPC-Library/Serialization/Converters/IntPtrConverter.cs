namespace IPPCLibrary.Serialization.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class IntPtrConverter : JsonConverter<nint>
{
    public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? str = reader.GetString();

        if (str == null)
            throw new Exception("Cannot convert null to Quaternion");

        return nint.Parse(str);
    }

    public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}