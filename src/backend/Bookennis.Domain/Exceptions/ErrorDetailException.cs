namespace Bookennis.Domain.Exceptions;

public abstract class ErrorDetailException(string? errorCode, string[]? errorDetails, string message) : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;
    public string[]? ErrorDetails { get; } = errorDetails;

    protected ErrorDetailException(string message)
        : this((string?)null, null, message)
    { }

    protected ErrorDetailException(Enum? errorCode, string[]? errorDetails, string message)
        : this(errorCode?.ToString(), errorDetails, message)
    { }
}