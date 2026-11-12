namespace TransactionValidation.Gateway.Contracts.Interfaces;

public interface IGatewayCredentialVerifier
{
    bool IsValid(string credential);
}
