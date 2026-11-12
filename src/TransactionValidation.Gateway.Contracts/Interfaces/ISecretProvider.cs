namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface ISecretProvider
{
    ValueTask<byte[]> GetSecretAsync(string secretName, CancellationToken cancellationToken);
}
