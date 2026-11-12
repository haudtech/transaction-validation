namespace TransactionValidation.Gateway.Contracts.Models;

public enum GatewayCredentialVerificationStatus
{
    Invalid,
    Valid,
    Unavailable
}

public sealed record GatewayCredentialVerificationResult(GatewayCredentialVerificationStatus Status)
{
    public static GatewayCredentialVerificationResult Invalid { get; } =
        new(GatewayCredentialVerificationStatus.Invalid);

    public static GatewayCredentialVerificationResult Valid { get; } =
        new(GatewayCredentialVerificationStatus.Valid);

    public static GatewayCredentialVerificationResult Unavailable { get; } =
        new(GatewayCredentialVerificationStatus.Unavailable);
}
