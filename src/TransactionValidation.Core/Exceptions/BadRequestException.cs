namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Signals a client input problem, mapped to HTTP 400 by the global exception handler.
/// This follows the validation failure handling described in the solution analysis.
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
