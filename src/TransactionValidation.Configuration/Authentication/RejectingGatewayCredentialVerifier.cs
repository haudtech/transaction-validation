using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

namespace TransactionValidation.Configuration.Authentication;

public sealed class RejectingGatewayCredentialVerifier : IGatewayCredentialVerifier
{
    public ValueTask<GatewayCredentialVerificationResult> VerifyAsync(
        string credential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GatewayCredentialVerificationResult.Invalid);
    }
}
