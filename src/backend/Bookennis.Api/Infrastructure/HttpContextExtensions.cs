namespace Bookennis.Api.Infrastructure;

public static class HttpContextExtensions
{
    public static int? GetRequestTimeZoneOffset(this HttpContext httpContext)
    {
        if (int.TryParse(httpContext.Request.Headers.SingleOrDefault(i => i.Key == "x-timezone-offset").Value.FirstOrDefault(), out var timeZoneOffset))
            return timeZoneOffset;

        return null;
    }
}