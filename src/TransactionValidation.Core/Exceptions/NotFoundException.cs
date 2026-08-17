namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents a missing or unverified partner entity in the upstream verification flow.
/// It is translated to HTTP 404 by the centralized exception handler.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
