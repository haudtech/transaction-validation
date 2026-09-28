namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents a request that fails input or request-boundary validation and maps to HTTP 400.
/// </summary>
public sealed class BadRequestException : Exception
{
    /// <summary>
    /// Creates a validation or malformed-input exception that maps to HTTP 400.
    /// </summary>
    /// <param name="message">Human-readable error detail for the client.</param>
    public BadRequestException(string message) : base(message)
    {
    }
}
