namespace TransactionValidation.Configuration.Options;

public static class ServiceBusPublisherOptionsValidator
{
    public static ServiceBusPublisherOptions Validate(ServiceBusPublisherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var missingProperties = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionString) && string.IsNullOrWhiteSpace(options.Namespace))
        {
            missingProperties.Add($"{nameof(ServiceBusPublisherOptions.ConnectionString)} or {nameof(ServiceBusPublisherOptions.Namespace)}");
        }

        if (string.IsNullOrWhiteSpace(options.TopicName))
        {
            missingProperties.Add(nameof(ServiceBusPublisherOptions.TopicName));
        }

        if (string.IsNullOrWhiteSpace(options.Subject))
        {
            missingProperties.Add(nameof(ServiceBusPublisherOptions.Subject));
        }

        if (string.IsNullOrWhiteSpace(options.RoutingKey))
        {
            missingProperties.Add(nameof(ServiceBusPublisherOptions.RoutingKey));
        }

        if (string.IsNullOrWhiteSpace(options.EventType))
        {
            missingProperties.Add(nameof(ServiceBusPublisherOptions.EventType));
        }

        if (missingProperties.Count > 0)
        {
            throw new InvalidOperationException(
                $"Azure Service Bus publisher configuration is incomplete. Missing values: {string.Join(", ", missingProperties)}");
        }

        return options;
    }
}
