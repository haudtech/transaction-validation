# Messaging

## Purpose and Scope

This document defines the implemented asynchronous messaging behavior for TransactionValidation. It covers runtime broker selection, the shared publication contract, RabbitMQ and Azure Service Bus adapters, message metadata, topology ownership, independent consumers, acknowledgement/completion, redelivery, and the Mock runtime's E2E observation controls.

It does not define broker provisioning procedures, Azure deployment commands, API idempotency storage, or durable downstream business processing.

## Behavior Inventory

- Single active broker selection
- Transport-neutral publication contract
- Shared transaction envelope and broker metadata
- RabbitMQ persistent confirmed publication
- Azure Service Bus topic publication and authentication selection
- RabbitMQ runtime topology initialization and unroutable capture
- Service Bus infrastructure-owned topic and subscription topology
- Independent primary and audit delivery paths
- Manual acknowledgement or completion after observation
- Failure-before-acknowledgement redelivery testing
- Consumer resource recovery and graceful shutdown

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Broker selection | Microsoft dependency injection | One configuration-driven registration callback per broker | `BrokerRegistrationExtensions`, API and Mock `Program` | Broker registration and API startup tests |
| Publication contract | .NET interface | `IMessagePublisher` accepts a runtime-neutral `TransactionEnvelope` | Core and Messaging projects | Publisher and controller tests |
| RabbitMQ publication | RabbitMQ.Client 7 | Persistent topic-exchange publish on a confirmation-enabled channel | `RabbitMqMessagePublisher`, `RabbitMqClientAdapter` | Publisher unit tests and E2E accepted flow |
| RabbitMQ routing | Topic exchange | Outcome routing keys with wildcard and accepted-only bindings | Routing resolver and Mock consumers | Routing-key tests and E2E selective delivery |
| Unroutable capture | RabbitMQ alternate exchange | Fanout alternate exchange bound to an unrouted queue | `RabbitMqTopologyInitializer`, primary consumer | Runtime topology and E2E setup |
| Service Bus publication | Azure.Messaging.ServiceBus | Topic send with SDK and application metadata | `ServiceBusMessagePublisher`, sender and client factory | Publisher unit tests and E2E accepted flow |
| Service Bus authentication | Azure.Identity | Connection string when present; otherwise `DefaultAzureCredential` with namespace | `ServiceBusClientFactory` | Options validation, startup, and Azure runtime configuration |
| Service Bus routing | Azure Service Bus subscriptions | Infrastructure-owned SQL filters on `eventType` | Bicep Service Bus module | Bicep build and E2E selective delivery |
| Consumer processing | RabbitMQ polling and Service Bus processors | Separate destinations with manual ack/completion by default | Mock consumer services | E2E fan-out and redelivery tests |
| Runtime observation | Concurrent in-memory queues | Record delivery metadata and arm one-shot audit failure | Mock observation store/controller and failure control | E2E observation tests |

## Definitions and Semantics

### Active broker

`Messaging:BrokerType` selects `RabbitMq` or `AzureServiceBus`. RabbitMQ is the default when the section is absent. Unsupported values fail startup registration. The API and Mock apply the same selection helper so publisher and consumer paths use one broker mode per process.

### Publication contract

The API depends on `IMessagePublisher.PublishAsync(TransactionEnvelope, CancellationToken)`. Core does not expose exchange names, topic names, routing keys, queues, subscriptions, or broker SDK types.

A `TransactionEnvelope` carries:

- `MessageId`
- `CorrelationId`
- `ReceivedAt`
- the original transaction request
- `PartnerVerified`

### Broker metadata

Both publishers serialize the same envelope and add these logical metadata fields:

| Metadata | RabbitMQ | Azure Service Bus |
|---|---|---|
| Message identity | `message-id` header | SDK `MessageId` and `message-id` application property |
| Correlation identity | `correlation-id` header | SDK `CorrelationId` and `correlation-id` application property |
| Contract type | `message-type` header | `message-type` application property |
| Contract version | `message-version` header | `message-version` application property |
| Routing | AMQP routing key | `routingKey` and `eventType` application properties plus `Subject` |

The current RabbitMQ publisher does not set AMQP `MessageId` or `CorrelationId` properties; it places those values in headers. The E2E helper additionally sets the native properties for direct test publications.

### RabbitMQ publish success

The API uses one singleton `RabbitMqClientAdapter`. It lazily creates and reuses a connection and confirmation-enabled channel under an operation lock. Messages are persistent and published with `mandatory: true`.

Successful completion of RabbitMQ.Client 7 `BasicPublishAsync` on the confirmation-enabled channel is treated as broker acceptance. It does not prove that a consumer processed the message. Exceptions reset and dispose the shared channel/connection so the next operation creates fresh resources.

`PublishConfirmTimeoutSeconds` remains in configuration and the adapter constructor for compatibility, but RabbitMQ.Client 7 confirmation completion does not use that value directly.

### Azure Service Bus send success

The publisher creates a `ServiceBusMessage` and delegates `SendMessageAsync` to a shared sender. Successful completion means the client send completed; it does not prove subscription processing.

`ServiceBusClientFactory` uses a connection string when present. Otherwise it requires the namespace FQDN and creates the client with `DefaultAzureCredential`.

### Topology ownership

RabbitMQ topology is self-initializing in the current local runtime:

- the API topology initializer declares the alternate exchange, unrouted queue/binding, and main topic exchange
- the primary Mock consumer repeats the shared declarations and owns its primary queue/binding
- the audit Mock consumer owns its audit queue/binding and assumes the exchange exists

The initializer retries up to ten times with a fixed 500 ms delay. Its final failure is logged and does not stop host startup.

Azure Service Bus topology is provisioned by Bicep:

- topic `partner.transactions`
- primary subscription `partner-transactions`
- audit subscription `partner-transactions.audit`
- primary SQL rule for accepted, rejected, and pending event types
- audit SQL rule for accepted events only
- 30-second lock duration and maximum delivery count of 10

The consumer `Filter` option is present but is not applied by the consumer services; filtering is owned by the provisioned Service Bus rules.

### Independent delivery paths

RabbitMQ uses one queue per consumer. Service Bus uses one subscription per consumer. An accepted event can therefore be observed independently by primary and audit consumers. Sharing one queue or subscription would create competing consumption rather than fan-out.

The API publisher knows only the exchange or topic contract. It does not enumerate primary or audit destinations.

### Acknowledgement and completion

RabbitMQ consumers poll with `BasicGetAsync`. With the current `AutoAck=false` configuration, each consumer records the observation and calls `BasicAckAsync` afterward.

Service Bus consumers use `ServiceBusProcessor` in PeekLock mode. With the current `AutoComplete=false` configuration, each consumer records the observation and calls `CompleteMessageAsync` afterward.

The audit failure control removes a one-shot marker and throws after recording but before acknowledgement/completion. This permits redelivery and creates a second observation with redelivery/delivery-count metadata.

### Consumer recovery and shutdown

RabbitMQ consumer exceptions leave the consume loop, dispose the owned channel and connection, log a retry, wait two seconds, and start a new loop. Cancellation exits without retry.

Service Bus processors log errors through `ProcessErrorAsync`, remain active until cancellation, and call `StopProcessingAsync` during shutdown. SDK-level retry behavior is not customized by the application.

## Implementation Pattern Rules

### Pattern: Select one broker at composition time

- **Applies to** - API publisher and Mock consumers; cross-cutting.
- **Intent** - preserve one application contract while activating one transport implementation.
- **Rule** - broker selection MUST occur during service registration through `AddConfiguredBroker`. API and Mock configuration MUST select the same broker mode. Unsupported values MUST fail registration.
- **Approved code shape**:

```csharp
services.AddConfiguredBroker(
    configuration,
    RegisterRabbitMq,
    RegisterAzureServiceBus);
```

- **Avoid** - runtime broker branching in controllers, registering two active `IMessagePublisher` implementations, or transport selection in Core.
- **Implementation references** - `BrokerRegistrationExtensions`; API and Mock `Program`; `BrokerTypeOptions`.
- **Verification** - `BrokerRegistrationExtensionsTests` and Azure-broker startup integration test.

### Pattern: Publish through a transport-neutral contract

- **Applies to** - API-to-broker publication; broker-neutral.
- **Intent** - isolate transaction orchestration from SDK and topology details.
- **Rule** - the API MUST publish through `IMessagePublisher`. The contract MUST accept the shared envelope and cancellation token only; it MUST NOT expose queues, topics, routing keys, or broker SDK types.
- **Approved code shape**:

```csharp
public interface IMessagePublisher
{
    Task PublishAsync(
        TransactionEnvelope envelope,
        CancellationToken cancellationToken = default);
}
```

- **Avoid** - broker clients in controllers, publisher methods per consumer, or transport metadata in the Core interface.
- **Implementation references** - `IMessagePublisher`; both publisher implementations; `PartnerTransactionsController`.
- **Verification** - controller and publisher unit tests.

### Pattern: Preserve envelope identity across transports

- **Applies to** - RabbitMQ and Service Bus publishers; broker-neutral contract with broker-specific mapping.
- **Intent** - keep message and correlation identity available to consumers and diagnostics.
- **Rule** - publishers MUST serialize `TransactionEnvelope` and propagate message ID, correlation ID, contract type, and contract version in broker metadata. Service Bus filtering metadata MUST be set as application properties.
- **Approved code shape**:

```csharp
var metadata = new Dictionary<string, object>
{
    ["message-type"] = "PartnerTransactionAccepted",
    ["message-version"] = "1",
    ["correlation-id"] = envelope.CorrelationId,
    ["message-id"] = envelope.MessageId
};
```

- **Avoid** - generating a second message ID in an adapter, dropping correlation metadata, or changing the serialized envelope by broker.
- **Implementation references** - `RabbitMqMessagePublisher`; `ServiceBusMessagePublisher`; `TransactionEnvelope`.
- **Verification** - RabbitMQ and Service Bus publisher tests plus E2E fan-out assertions.

### Pattern: Confirm RabbitMQ publication before acceptance

- **Applies to** - RabbitMQ publisher; broker-specific.
- **Intent** - avoid returning API acceptance before RabbitMQ accepts the publication.
- **Rule** - RabbitMQ publishing MUST use a confirmation-enabled channel, persistent messages, and `BasicPublishAsync`. Publish exceptions or a false adapter result MUST prevent API acceptance.
- **Approved code shape**:

```csharp
var channel = await connection.CreateChannelAsync(
    new CreateChannelOptions(true, true, null, null),
    cancellationToken);

await channel.BasicPublishAsync(
    exchange,
    routingKey,
    mandatory: true,
    properties,
    body,
    cancellationToken);
```

- **Avoid** - fire-and-forget publication, per-request connection creation in the publisher, legacy confirm-selection APIs, or treating broker acceptance as consumer completion.
- **Implementation references** - `RabbitMqClientAdapter`; `RabbitMqMessagePublisher`.
- **Verification** - RabbitMQ publisher tests and E2E accepted flow.

### Pattern: Send Service Bus messages through a reusable sender

- **Applies to** - Azure Service Bus publisher; broker-specific.
- **Intent** - isolate SDK client lifetime and message construction from API orchestration.
- **Rule** - the publisher MUST build one `ServiceBusMessage`, set native and application metadata, and delegate to `IServiceBusMessageSender`. Client creation MUST prefer a supplied connection string and otherwise use namespace plus `DefaultAzureCredential`.
- **Approved code shape**:

```csharp
var message = new ServiceBusMessage(JsonSerializer.Serialize(envelope))
{
    MessageId = envelope.MessageId,
    CorrelationId = envelope.CorrelationId,
    Subject = subject,
    To = topicName
};

message.ApplicationProperties["eventType"] = eventType;
await sender.SendMessageAsync(message, cancellationToken);
```

- **Avoid** - creating a sender per controller request, omitting filter metadata, or requiring a connection string in Azure-hosted execution.
- **Implementation references** - `ServiceBusMessagePublisher`; `ServiceBusMessageSender`; `ServiceBusClientFactory`; options validator.
- **Verification** - Service Bus publisher and options-validator tests.

### Pattern: Keep broker topology outside the request path

- **Applies to** - RabbitMQ startup and Azure infrastructure; broker-specific.
- **Intent** - keep topology creation out of transaction request processing.
- **Rule** - RabbitMQ shared topology MUST be declared by hosted startup/consumer initialization, not by `PublishAsync`. Azure Service Bus topology MUST be provisioned by Bicep and MUST NOT be created by consumers at runtime.
- **Approved code shape**:

```csharp
services.AddHostedService<RabbitMqTopologyInitializer>();

// Azure topic, subscriptions, and rules are declared in Bicep.
```

- **Avoid** - declaring queues on every publish, API knowledge of consumer destinations, or runtime Service Bus subscription creation.
- **Implementation references** - `RabbitMqTopologyInitializer`; RabbitMQ primary consumer; Service Bus Bicep module.
- **Verification** - publisher test asserting no queue declaration, source review, Bicep build, and E2E topology behavior.

### Pattern: Give independent consumers independent destinations

- **Applies to** - primary and audit paths; broker-neutral architecture.
- **Intent** - deliver one publication independently to consumers with different interests and failure states.
- **Rule** - independent consumers MUST use distinct RabbitMQ queues or Service Bus subscriptions. The primary destination SHOULD match the broad transaction-event set; the audit destination MUST match accepted events only in the current topology.
- **Approved code shape**:

```text
one publication
    -> primary queue/subscription
    -> audit queue/subscription
```

- **Avoid** - sharing one queue for fan-out, publishing separately per consumer, or hard-coding consumer names in the API.
- **Implementation references** - RabbitMQ consumer options; Service Bus Bicep subscriptions/rules; Mock consumer services.
- **Verification** - E2E fan-out and selective-routing tests.

### Pattern: Complete only after successful handling

- **Applies to** - RabbitMQ and Service Bus consumers; broker-specific acknowledgement APIs.
- **Intent** - preserve redelivery when processing fails before completion.
- **Rule** - with manual acknowledgement/completion enabled, consumers MUST deserialize and perform their implemented handling before `BasicAckAsync` or `CompleteMessageAsync`. Failure before that boundary MUST propagate without acknowledging the message.
- **Approved code shape**:

```csharp
var envelope = Deserialize(message);
observationStore.Add(CreateObservation(envelope));

if (failureControl.ShouldFailBeforeAcknowledgement(consumer, envelope.MessageId))
{
    throw new InvalidOperationException("Configured failure before acknowledgement.");
}

await CompleteOrAcknowledgeAsync(message, cancellationToken);
```

- **Avoid** - acknowledging before deserialization/handling, swallowing processing failures, or assuming broker redelivery provides business deduplication.
- **Implementation references** - all four consumer services; `ConsumerFailureControl`.
- **Verification** - E2E audit redelivery and primary-path isolation test.

### Pattern: Keep observation and failure controls test-only

- **Applies to** - Mock runtime E2E support; environment-specific.
- **Intent** - provide deterministic evidence of routing and redelivery without presenting the Mock as a durable business consumer.
- **Rule** - observation and forced-failure controls MUST remain in the Mock runtime and MUST NOT be exposed by the partner API. Feature documentation MUST describe them as in-memory test support, not audit persistence or consumer deduplication.
- **Approved code shape**:

```csharp
observationStore.Add(observation);

if (failureControl.ShouldFailBeforeAcknowledgement(consumerName, messageId))
{
    throw new InvalidOperationException("Configured failure before acknowledgement.");
}
```

- **Avoid** - using the observation store as a production audit database, claiming it deduplicates messages, or placing failure controls in the public API.
- **Implementation references** - `ConsumerObservationStore`; `ConsumerFailureControl`; `ConsumerObservationController`.
- **Verification** - E2E observation, fan-out, selective-routing, and redelivery tests.

## Configuration Contract

### Broker selection

| Section/key | Default | Purpose |
|---|---|---|
| `Messaging:BrokerType` | `RabbitMq` | Selects `RabbitMq` or `AzureServiceBus`. |

### RabbitMQ publisher

| Section/key | Default | Purpose |
|---|---:|---|
| `RabbitMq:HostName` | `localhost` | Broker host. |
| `RabbitMq:Port` | `5672` | AMQP port. |
| `RabbitMq:UserName` | `guest` | Broker user for local defaults. |
| `RabbitMq:Password` | `guest` | Broker password for local defaults; deployed values must be secret-backed. |
| `RabbitMq:ExchangeName` | `partner.transactions` | Main topic exchange. |
| `RabbitMq:ExchangeType` | `topic` | Main exchange type. |
| `RabbitMq:RoutingKeyPrefix` | `partner.transaction` | Prefix for accepted/unverified routing keys. |
| `RabbitMq:AlternateExchangeName` | `partner.transactions.unrouted` | Captures unmatched routing keys. |
| `RabbitMq:UnroutedQueueName` | `partner-transactions.unrouted` | Stores alternate-exchange traffic. |
| `RabbitMq:Durable` | `true` | Controls declared topology durability. |
| `RabbitMq:PublishConfirmTimeoutSeconds` | `5` | Retained compatibility setting; not directly used by the RabbitMQ.Client 7 publish path. |

RabbitMQ primary and audit consumers use separate `RabbitMqConsumer` and `RabbitMqAuditConsumer` sections for enablement, connection settings, queue, exchange, binding, durability, acknowledgement, and polling interval.

### Service Bus publisher and consumers

| Section/key | Rule | Purpose |
|---|---|---|
| `ServiceBusPublisher:ConnectionString` | Optional when namespace is supplied | Local or explicit Service Bus authentication. |
| `ServiceBusPublisher:Namespace` | Required when connection string is empty | Namespace FQDN for `DefaultAzureCredential`. |
| `ServiceBusPublisher:TopicName` | Required | Publish destination. |
| `ServiceBusPublisher:Subject` | Required | Native message subject. |
| `ServiceBusPublisher:RoutingKey` | Required | Routing metadata. |
| `ServiceBusPublisher:EventType` | Required | Subscription-filter metadata. |
| `ServiceBusConsumer` / `ServiceBusAuditConsumer` | Separate sections | Enable and configure primary/audit processors. |
| Consumer `AutoComplete` | `false` in current settings | Uses explicit completion after handling. |
| Consumer `MaxConcurrentCalls` | `1` in current settings | Limits processor concurrency. |
| Consumer `Filter` | Present but not applied by consumer code | Bicep SQL rules own filtering. |

## Source Ownership

| Project or surface | Responsibility |
|---|---|
| `TransactionValidation.Core` | `IMessagePublisher`, `TransactionEnvelope`, and broker-neutral logging contracts. |
| `TransactionValidation.Configuration` | Broker selection and publisher option models/validation. |
| `TransactionValidation.Api` | Concrete publisher composition and publish orchestration. |
| `TransactionValidation.Messaging` | RabbitMQ and Service Bus publishers, clients, routing resolver, and RabbitMQ topology initializer. |
| `TransactionValidation.Mock` | Primary/audit consumer services and E2E observation/failure controls. |
| `infra/bicep/modules/servicebus.bicep` | Service Bus namespace, topic, subscriptions, SQL rules, delivery count, and private endpoint. |
| `TransactionValidation.Tests` | Publisher, routing, registration, fan-out, selective routing, and redelivery verification. |

## Verification Surface

| Verification | Proves |
|---|---|
| `BrokerRegistrationExtensionsTests` | Default, Service Bus, missing callback, and unsupported broker selection. |
| `RabbitMqMessagePublisherTests` | One exchange publish, routing/header metadata, no per-request queue declaration, and failed-confirm handling. |
| `PartnerTransactionRoutingKeyResolverTests` | Accepted/unverified outcome keys and configured prefix handling. |
| `ServiceBusMessagePublisherTests` | Service Bus subject, routing/filter metadata, identity metadata, and failure propagation. |
| `ServiceBusPublisherOptionsValidatorTests` | Required topic, metadata, and connection-string-or-namespace contract. |
| API startup integration test | Azure Service Bus publisher composition can start behind the shared contract. |
| E2E fan-out test | One accepted publication is observed by both independent destinations. |
| E2E selective-routing test | An unverified direct test publication is observed only by primary. |
| E2E redelivery test | Audit failure before acknowledgement/completion creates a later redelivered observation while primary remains independent. |
| Bicep build | Service Bus topic, subscriptions, SQL rules, delivery limits, and private networking compile. |

## Operational Boundaries

- The API transaction workflow publishes accepted envelopes; unverified routing is exercised by the routing resolver and direct E2E broker publication, not by the normal accepted API path.
- Consumers record observations in process memory for test visibility. They do not perform durable business or audit writes.
- Consumers do not currently deduplicate by message ID. At-least-once redelivery can therefore produce multiple observations and would require an idempotent durable handler before non-idempotent production side effects.
- RabbitMQ has an alternate exchange and unrouted queue, not a consumer poison-message DLQ. No basic-return warning handler is implemented.
- Service Bus subscriptions use `maxDeliveryCount: 10`; the application does not implement a dead-letter processor.
- RabbitMQ shared topology is declared by both the API initializer and primary Mock consumer in local runtime. Service Bus topology is infrastructure-owned.
- Service Bus consumer `Filter` configuration is not applied at runtime; Bicep SQL rules are authoritative.
- Publisher completion proves broker/client acceptance, not consumer completion.
- The current consumer services run together inside the Mock process, so destination-level failure isolation is demonstrated but process-level isolation is not.
- Message schema compatibility is represented by `message-type` and `message-version` metadata, but no schema registry or version-dispatch framework is implemented.

## Related Supporting Documents

- [Architecture design overview](../architecture_design/Architecture_design.md)
- [Messaging topology and consumer routing](../architecture_design/messaging_topology_and_consumer_routing.md)
- [Message processing lifecycle](../architecture_design/messaging_topology_and_consumer_routing.md#11-message-processing-lifecycle)
- [Runtime and API](../runtime-and-api/README.md)
- [Reliability](../reliability/README.md)
