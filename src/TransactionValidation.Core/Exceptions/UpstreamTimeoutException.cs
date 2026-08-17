namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents an outbound timeout while contacting the upstream partner verification endpoint.
/// This corresponds to the HTTP 408 timeout handling described in the solution analysis.
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
