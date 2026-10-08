namespace Bookennis.Api.Infrastructure.Tenant;

public class MultiTenantServiceMiddleware(ITenantService tenantService) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = ITenantService.GetTenantIdFromPath(context.Request.Path);

        if (tenantId is not null)
            tenantService.SetTenantId(tenantId.Value);

        await next(context);
    }
}