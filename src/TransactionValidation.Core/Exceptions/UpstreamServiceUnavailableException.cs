namespace TransactionValidation.Core.Exceptions;

public sealed class UpstreamServiceUnavailableException : Exception
{
    public UpstreamServiceUnavailableException(string message) : base(message)
    {
    }
}
