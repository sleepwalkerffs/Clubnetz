namespace Bookennis.Api.Infrastructure.Exceptions;

public sealed class AccessDeniedException(string message) : Exception(message)
{
}
