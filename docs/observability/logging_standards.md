# Logging Standards

This guide defines the implementation rules for structured application logging across the solution.

## Principle

Production logging uses .NET source-generated logging through static partial logging catalogs. Direct calls to `ILogger` extension methods are not permitted in production code.

The built-in analyzer rules in `.editorconfig` enforce this style:

- `CA1848` requires source-generated logging methods instead of direct logging extension methods.
- `CA2254` requires compile-time logging message templates.

Both rules are configured as build errors.

## Logging Catalogs

Logging events are grouped by ownership:

| Catalog | Responsibility | Event range |
|---|---|---|
| `TransactionValidationLogging` | Transaction processing, validation, partner verification, and idempotency | `1000-1299` |
| `MessagingLogging` | Broker-neutral publishing and consumer events | `1300-1399` |
| `RabbitMqLogging` | RabbitMQ topology and consumer lifecycle events | `1400-1404` |
| `ServiceBusLogging` | Azure Service Bus consumer lifecycle events | `1405-1406` |

Catalogs live under `src/TransactionValidation.Core/Logging` and use `[LoggerMessage]` methods with stable event IDs and typed parameters.

## Usage

Pass the owning class's typed logger to the catalog method:

```csharp
TransactionValidationLogging.TransactionPublished(
    _logger,
    correlationId,
    partnerId,
    transactionReference,
    messageId,
    durationMs);
```

The caller should inject `ILogger<TClass>`. This preserves the caller's type as the log category while keeping event definitions centralized.

Do not create an interface, injected wrapper service, or shared logger instance solely for these catalogs. The catalogs are stateless compile-time event definitions, not replaceable runtime services.

## Event Design Rules

- Use a meaningful event-specific method name.
- Assign a stable event ID from the appropriate range.
- Do not reuse an event ID for a different meaning.
- Keep structured property names consistent and use PascalCase in message templates.
- Keep `[LoggerMessage]` arguments one per line.
- Keep method parameters one per line.
- Pass exceptions through the `Exception` parameter when the event represents a failure or retry.
- Log operational context, not secrets or sensitive payload data.
- Do not log API keys, passwords, connection strings, access tokens, or private credentials.
- Prefer a small number of useful events over logging every implementation step.

## Adding an Event

1. Choose the catalog that owns the event.
2. Select an unused event ID from its range.
3. Add a source-generated `[LoggerMessage]` method with typed parameters.
4. Update the relevant call site to use the catalog.
5. Add or update focused logging tests when the event contract is important.
6. Run the formatting and build checks.

Example declaration:

```csharp
[LoggerMessage(
    EventId = 1400,
    Level = LogLevel.Information,
    Message = "RabbitMQ exchange declared. Exchange={ExchangeName} Type={ExchangeType}")]
public static partial void ExchangeDeclared(
    ILogger logger,
    string exchangeName,
    string exchangeType);
```

## Validation

Run the repository quality checks from the workspace root:

```bash
dotnet format --verify-no-changes --verbosity diagnostic TransactionValidation.sln
dotnet build --nologo -m:1 TransactionValidation.sln
```

A direct call such as the following should fail `CA1848`:

```csharp
_logger.LogInformation("Message published: {MessageId}", messageId);
```

Replace it with the appropriate source-generated catalog event.

## Related Documentation

- [Observability Decisions and Implementation Plan](observability_decisions_and_implementation_plan.md)
- [Shared Engineering Principles](../implementation/shared_engineering_principles.md)
