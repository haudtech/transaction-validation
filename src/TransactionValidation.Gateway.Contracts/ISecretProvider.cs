namespace TransactionValidation.Gateway.Contracts;

public interface ISecretProvider
{
    ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken);
}
