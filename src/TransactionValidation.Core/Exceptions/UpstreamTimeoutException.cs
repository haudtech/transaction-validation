namespace TransactionValidation.Core.Exceptions;

public sealed class UpstreamTimeoutException : Exception
{
    public UpstreamTimeoutException(string message) : base(message)
    {
    }
}
