namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents a transient upstream dependency failure, such as a partner or broker service outage.
/// This is mapped to HTTP 503, consistent with the resilience guidance in the design docs.
/// </summary>
public sealed class UpstreamServiceUnavailableException : Exception
{
    /// <summary>
    /// Creates an upstream dependency failure exception used for 503 responses from the partner verification or queueing layer.
    /// </summary>
    /// <param name="message">The upstream-error message to expose to the client.</param>
    public UpstreamServiceUnavailableException(string message) : base(message)
    {
    }
}
