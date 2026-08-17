namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Signals a client input problem, mapped to HTTP 400 by the global exception handler.
/// This follows the validation failure handling described in the solution analysis.
/// </summary>
public sealed class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}
