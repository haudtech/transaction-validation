namespace TransactionValidation.Core.Exceptions;

/// <summary>
/// Represents an outbound timeout while contacting the upstream partner verification endpoint.
/// This corresponds to the HTTP 408 timeout handling described in the solution analysis.
/// </summary>
public sealed class UpstreamTimeoutException : Exception
{
    public UpstreamTimeoutException(string message) : base(message)
    {
    }
}
