using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface IGatewayCredentialVerifier
{
    ValueTask<GatewayCredentialVerificationResult> VerifyAsync(
        string credential,
        CancellationToken cancellationToken);
}
