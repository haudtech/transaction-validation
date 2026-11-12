namespace TransactionValidation.Gateway.Management.Options;

public sealed class BackendCredentialManagementOptions
{
    public const string SectionName = "Gateway:BackendCredentialManagement";

    public string PepperVersion { get; set; } = string.Empty;
}
