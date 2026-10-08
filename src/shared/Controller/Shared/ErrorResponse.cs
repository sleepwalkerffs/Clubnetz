namespace Bookennis.Shared.Controller.Shared;

public class ErrorCodeResponse(string message, string? errorCode, string[]? errorDetail)
{
    public string Message { get; } = message;
    public string? ErrorCode { get; } = errorCode;
    public string[]? ErrorDetail { get; } = errorDetail;
}