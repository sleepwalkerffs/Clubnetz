using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Api.Infrastructure.JsonConverters;
using Bookennis.Api.Infrastructure.Swagger;
using Bookennis.Api.Shared.AspNetCore;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class ControllerRoutingConfiguration
{
    public static void AddControllerRouting(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services
            .AddMvc(options => options.Filters.Add<ExceptionFilter>())
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.Converters.Add(new DateTimeOffsetJsonConverter());
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            })
            .AddViewLocalization();

        services.AddApiVersioning()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(x => x.FullName?.Replace("+", "_"));
            options.SchemaFilter<IgnoreReadOnlySchemaFilter>();
        });

        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("https://localhost:7026").AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
    }
}
