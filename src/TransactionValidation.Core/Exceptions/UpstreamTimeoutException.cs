namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents an upstream dependency timeout that maps to the API timeout response category.
/// </summary>
public sealed class UpstreamTimeoutException : Exception
{
    /// <summary>
    /// Creates a timeout exception representing an upstream verification failure that should map to HTTP 408.
    /// </summary>
    /// <param name="message">The timeout detail returned to the caller.</param>
    public UpstreamTimeoutException(string message) : base(message)
    {
    }
}
