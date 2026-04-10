using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NetcodePatcher.Build.Util;

public class JsonNuGetVersionConverter : JsonConverter<NuGetVersion>
{
    public override NuGetVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => NuGetVersion.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, NuGetVersion value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
