using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.JsonConverters;
using Bookennis.Api.Infrastructure.Swagger;
using Bookennis.Api.Shared.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class ControllerRoutingConfiguration
{
    public static void AddControllerRouting(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services
            .AddMvc(options =>
            {
                options.Filters.Add<ExceptionFilter>();
                options.Filters.Add<ClubApiKeyReadOnlyFilter>();
            })
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
            options.OperationFilter<OperationNameFilter>();

            // Summaries of controllers, actions and models (the XML files are built next to the assemblies)
            foreach (var xmlFile in Directory.EnumerateFiles(AppContext.BaseDirectory, "Bookennis.*.xml"))
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);

            // The document with all endpoints stays local, the one for club API keys is published (see Program.cs)
            if (builder.Environment.IsDevelopment())
                options.SwaggerDoc(ClubApiDocument.AllEndpointsName, new OpenApiInfo { Title = "Clubnetz (all endpoints)", Version = "1" });

            options.SwaggerDoc(ClubApiDocument.Name, ClubApiDocument.Info);
            options.AddSecurityDefinition(ClubApiDocument.SecurityScheme, ClubApiDocument.SecuritySchemeDefinition);
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ClubApiDocument.SecurityScheme, document)] = [],
            });
        });

        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<AuthorizationOptions>>((options, authorization) =>
                options.DocInclusionPredicate((documentName, api) => documentName != ClubApiDocument.Name || ClubApiDocument.Includes(api, authorization.Value)));

        services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("https://localhost:7026").AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
    }
}
