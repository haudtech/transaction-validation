namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface ISecretWriter
{
    ValueTask WriteSecretAsync(
        string secretName,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken);

    ValueTask DeleteSecretAsync(string secretName, CancellationToken cancellationToken);
}
