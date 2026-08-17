namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents a transient upstream dependency failure, such as a partner or broker service outage.
/// This is mapped to HTTP 503, consistent with the resilience guidance in the design docs.
/// </summary>
public sealed class UpstreamServiceUnavailableException : Exception
{
    public UpstreamServiceUnavailableException(string message) : base(message)
    {
    }
}
