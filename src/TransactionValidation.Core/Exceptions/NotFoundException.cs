namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents a missing or unverified partner entity in the upstream verification flow.
/// It is translated to HTTP 404 by the centralized exception handler.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Creates a not-found exception for an unknown or unavailable partner in the verification flow.
    /// </summary>
    /// <param name="message">The error detail for the caller.</param>
    public NotFoundException(string message) : base(message)
    {
    }
}
