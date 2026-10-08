namespace Bookennis.Api.Infrastructure;

public static class HttpContextAccessorExtension
{
    public static int GetRequestTimeZoneOffset(this IHttpContextAccessor accessor)
    {
        if (accessor.HttpContext is null)
            return 0;

        return accessor.HttpContext.GetRequestTimeZoneOffset() ?? 0;
    }
}