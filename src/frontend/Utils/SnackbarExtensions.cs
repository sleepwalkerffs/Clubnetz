using System.Net;
using Bookennis.Client.Services.HttpClients;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Bookennis.Client.Utils;

public static class SnackbarExtensions
{
    public static void ShowErrorDetails(this ISnackbar snackbar, HttpResult response, IStringLocalizer locale)
    {
        string errorToDisplay;
        var error = response.ErrorCodeResponse;
        if (error is { ErrorCode: "IdentityError", ErrorDetail: not null })
        {
            var errors = error.ErrorDetail.Select(x => locale[x].Value).ToList();
            //translate codes here to meaningful responses for user via resx file
            errorToDisplay = errors.Count != 0 ? string.Join(Environment.NewLine, errors) : error.Message;
        }
        // 429 comes from the rate limiter (TooManyRequests)
        else if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.TooManyRequests && error?.ErrorCode is not null)
        {
            errorToDisplay = locale.LocalizeErrorCode(error.ErrorCode, error.ErrorDetail);
        }
        else
        {
            // No error code: a text for the user instead of the bare status code
            errorToDisplay = response.StatusCode == HttpStatusCode.NotFound ? locale["Error_NotFound"] : locale["Error_Unexpected"];
        }

        snackbar.Add(errorToDisplay, Severity.Error);
    }

    /// <summary>
    /// The message of a backend error code (the key in <c>BackendErrorCodeLocale</c>) with its error details as format arguments,
    /// e.g. for the per-slot errors of a batch booking.
    /// </summary>
    public static string LocalizeErrorCode(this IStringLocalizer locale, string errorCode, string[]? errorDetails)
        => errorDetails is not null
            ? locale[errorCode, errorDetails]
            : locale[errorCode];
}
