using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Client.Infrastructure.JsonConverters;

namespace Bookennis.Client.Infrastructure.Configuration;

public static class ConfigureJson
{
    public static JsonSerializerOptions ConfigureJsonOptions(this JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new DateTimeOffsetJsonConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}