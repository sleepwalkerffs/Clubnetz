using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookennis.Api.Infrastructure.JsonConverters;

// There is no setting so we need a custom converter https://github.com/dotnet/runtime/issues/1566
public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}