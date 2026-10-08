using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookennis.Api.Infrastructure.Swagger;

public class IgnoreReadOnlySchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is OpenApiSchema concreteSchema)
        {
            concreteSchema.ReadOnly = false;
            if (concreteSchema.Properties != null)
            {
                foreach (var keyValuePair in concreteSchema.Properties)
                {
                    if (keyValuePair.Value is OpenApiSchema propSchema)
                    {
                        propSchema.ReadOnly = false;
                    }
                }
            }
        }
    }
}
