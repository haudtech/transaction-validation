namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Signals a duplicate or conflicting request, such as idempotency-key reuse with a different payload.
/// This is mapped to HTTP 409 by the API exception middleware.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
