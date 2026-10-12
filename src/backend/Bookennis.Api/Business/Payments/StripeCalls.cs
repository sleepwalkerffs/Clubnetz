using Bookennis.Domain.Exceptions;
using Stripe;

namespace Bookennis.Api.Business.Payments;

public static class StripeCalls
{
    /// <summary>
    /// Runs a call to Stripe for a request of a user. When Stripe declines it or can not be reached, the user gets
    /// a localized error (<see cref="PaymentErrorCode.PaymentProviderUnavailable"/>) and stays on the page instead of an error page.
    /// </summary>
    public static async Task<T> Run<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception e) when (e is StripeException or HttpRequestException or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new PreconditionException(PaymentErrorCode.PaymentProviderUnavailable, [e.Message], "The payment provider declined the request or could not be reached.");
        }
    }
}
