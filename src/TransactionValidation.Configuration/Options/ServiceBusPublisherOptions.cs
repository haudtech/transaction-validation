namespace TransactionValidation.Configuration.Options;

/// <summary>
/// Azure Service Bus topic publisher options used when the application is configured to use the Azure-native messaging path.
/// </summary>
public sealed class ServiceBusPublisherOptions
{
    public const string SectionName = "ServiceBusPublisher";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Namespace FQDN used for Managed Identity auth when <see cref="ConnectionString"/> is not set.</summary>
    public string Namespace { get; set; } = string.Empty;

    public string TopicName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;
}
