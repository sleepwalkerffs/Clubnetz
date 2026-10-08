using System.Net;
using Bookennis.Client.Services.HttpClients;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Bookennis.Client.Utils;

public static class SnackbarExtensions
{
    public static void ShowErrorDetails(this ISnackbar snackbar, HttpResult response, IStringLocalizer locale)
    {
        var errorToDisplay = string.Empty;
        if (response.ErrorCodeResponse is not null)
        {
            if (response.ErrorCodeResponse.ErrorDetail is not null && response.ErrorCodeResponse.ErrorCode == "IdentityError")
            {
                var errors = response.ErrorCodeResponse.ErrorDetail.Select(x => locale[x]);
                //translate codes here to meaningful responses for user via resx file
                errorToDisplay = errors.Any() ? string.Join(Environment.NewLine, errors) : response.ErrorCodeResponse.Message;
            }
            else
            {
                // 429 comes from the rate limiter (TooManyRequests)
                if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.TooManyRequests && response.ErrorCodeResponse.ErrorCode is not null)
                {
                    if (response.ErrorCodeResponse.ErrorDetail is not null)
                        errorToDisplay = locale[response.ErrorCodeResponse.ErrorCode, response.ErrorCodeResponse.ErrorDetail];
                    else
                        errorToDisplay = locale[response.ErrorCodeResponse.ErrorCode];
                }
                else
                {
                    errorToDisplay = $"ErrorCode: {response.StatusCode}";
                }

            }
        }

        snackbar.Add(errorToDisplay, Severity.Error);
    }
}
