namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Signals a duplicate or conflicting request, such as idempotency-key reuse with a different payload.
/// This is mapped to HTTP 409 by the API exception middleware.
/// </summary>
public sealed class ConflictException : Exception
{
    /// <summary>
    /// Creates a conflict exception used when a duplicate request or idempotency-key mismatch is detected.
    /// </summary>
    /// <param name="message">The conflict detail returned to the caller.</param>
    public ConflictException(string message) : base(message)
    {
    }
}
