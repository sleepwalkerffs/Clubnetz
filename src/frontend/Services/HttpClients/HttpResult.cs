using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.HttpClients;

public record HttpResult(bool Success, HttpStatusCode StatusCode, ErrorCodeResponse? ErrorCodeResponse)
{
    public static HttpResult OkResult => new(true, HttpStatusCode.OK, null);
};

public record HttpResult<T>(bool Success, HttpStatusCode StatusCode, ErrorCodeResponse? ErrorCodeResponse, T? Dto) : HttpResult(Success, StatusCode, ErrorCodeResponse);

public record FileDownload(byte[] Data, string FileName, string ContentType);

public static class HttpResultExtensions
{
    public static async Task<HttpResult<T>> AsHttpResult<T>(this HttpResponseMessage httpResponse, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken = default)
    {
        if (!httpResponse.IsSuccessStatusCode)
        {
            try
            {
                var errorResponse = await httpResponse.Content.ReadFromJsonAsync<ErrorCodeResponse>(jsonOptions, cancellationToken);
                return new(httpResponse.IsSuccessStatusCode, httpResponse.StatusCode, errorResponse, default);
            }
            catch (JsonException)
            {
                return new(httpResponse.IsSuccessStatusCode, httpResponse.StatusCode, null, default);
            }
        }

        return new(httpResponse.IsSuccessStatusCode, httpResponse.StatusCode, null, await httpResponse.Content.ReadFromJsonAsync<T>(jsonOptions, cancellationToken));
    }

    /// <summary>Reads a file response (e.g. an export) including the file name from the Content-Disposition header.</summary>
    public static async Task<HttpResult<FileDownload>> AsFileHttpResult(this HttpResponseMessage httpResponse, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken = default)
    {
        if (!httpResponse.IsSuccessStatusCode)
        {
            return await httpResponse.AsHttpResult<FileDownload>(jsonOptions, cancellationToken);
        }

        var headers = httpResponse.Content.Headers;
        var fileName = (headers.ContentDisposition?.FileNameStar ?? headers.ContentDisposition?.FileName ?? "download").Trim('"');
        var contentType = headers.ContentType?.MediaType ?? "application/octet-stream";
        var data = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        return new(true, httpResponse.StatusCode, null, new FileDownload(data, fileName, contentType));
    }

    public static async Task<HttpResult> AsHttpResult(this HttpResponseMessage httpResponse, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken = default) =>
        new(httpResponse.IsSuccessStatusCode,
            httpResponse.StatusCode,
            httpResponse.IsSuccessStatusCode ? null : await httpResponse.Content.ReadFromJsonAsync<ErrorCodeResponse>(jsonOptions, cancellationToken));
}