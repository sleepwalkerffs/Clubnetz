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
            ? locale[errorCode, FormatMoneyDetails(errorCode, errorDetails)]
            : locale[errorCode];

    /// <summary>Error codes whose first detail is an amount (invariant, e.g. "12.50"): it is shown in the club's currency and format.</summary>
    private static readonly HashSet<string> MoneyErrorCodes = ["AccountingAmountInvalid", "AccountingBankTransactionAmountMismatch"];

    private static object[] FormatMoneyDetails(string errorCode, string[] errorDetails)
    {
        var details = errorDetails.Cast<object>().ToArray();
        if (MoneyErrorCodes.Contains(errorCode) && details.Length > 0
            && decimal.TryParse(errorDetails[0], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            details[0] = Bookennis.Shared.Utils.RegionalCulture.Current.Money(amount);
        return details;
    }
}
