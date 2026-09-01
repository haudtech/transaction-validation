namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Selects the active broker implementation at runtime while keeping the inactive broker configuration available for future migration work.
/// </summary>
public sealed class BrokerTypeOptions
{
    public const string SectionName = "Messaging";

    public const string RabbitMq = "RabbitMq";

    public const string AzureServiceBus = "AzureServiceBus";

    public string BrokerType { get; set; } = RabbitMq;
}
