namespace Bookennis.Domain.Exceptions;

[Serializable]
public class PreconditionException : ErrorDetailException
{
    public PreconditionException(string message)
        : base(message)
    { }

    public PreconditionException(Enum? errorCode, string message)
        : base(errorCode, null, message)
    { }

    public PreconditionException(Enum? errorCode, string[] errorDetails, string message)
        : base(errorCode, errorDetails, message)
    { }
}