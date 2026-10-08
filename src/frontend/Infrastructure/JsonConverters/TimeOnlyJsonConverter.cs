using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Global;

namespace Bookennis.Client.Infrastructure.JsonConverters;

// There is no setting so we need a custom converter https://github.com/dotnet/runtime/issues/1566
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value.CleanNullable() == null)
            throw new FormatException("String is not in TimeOnlyFormat");

        var timeOnly = TimeOnly.FromDateTime((DateTime.UtcNow.Date + TimeOnly.Parse(value!).ToTimeSpan()).ToLocalTime());

        return timeOnly;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        var timeOnly = TimeOnly.FromDateTime((DateTime.Now.Date + value.ToTimeSpan()).ToUniversalTime());
        writer.WriteStringValue(timeOnly.ToString("r"));
    }
}