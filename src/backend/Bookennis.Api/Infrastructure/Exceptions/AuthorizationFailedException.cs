namespace Bookennis.Api.Infrastructure.Exceptions;

public class AuthorizationFailedException : Exception
{
    public AuthorizationFailedException()
        : this("Authorization failed") { }

    public AuthorizationFailedException(string? message)
        : base(message) { }

    public AuthorizationFailedException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
