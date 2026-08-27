# Adding a RabbitMQ Consumer

This runbook describes the local topic-exchange consumer model used by the TransactionValidation multiple-consumer POC.

## Topology

The publisher sends one message to the durable topic exchange:

```text
partner.transactions
```

Each consumer owns a separate durable queue and binding:

| Consumer | Queue | Binding |
|---|---|---|
| Primary Mock consumer | `partner-transactions` | `partner.transaction.#` |
| Audit Mock consumer | `partner-transactions.audit` | `partner.transaction.accepted` |

Do not make two independent consumers share a queue. A shared queue creates competing-consumer behavior, where each message is delivered to only one consumer.

## Add a Consumer

1. Choose a unique durable queue name owned by the consumer.
2. Choose the routing pattern that expresses the consumer's interest.
3. Bind the queue to `partner.transactions`.
4. Consume with manual acknowledgement unless the message is intentionally disposable.
5. Acknowledge only after successful processing.
6. Handle redelivery safely because delivery is at least once.
7. Deduplicate using the envelope `message-id` before applying a non-idempotent side effect.
8. Include `correlation-id`, `message-id`, and routing key in structured logs.
9. Add a dead-letter strategy for poison or permanently failed messages.
10. Add a broker-backed test proving the queue receives the intended message categories.

## Message Contract

Messages use the `TransactionEnvelope` JSON contract and include these headers:

| Header | Purpose |
|---|---|
| `message-type` | Logical event name |
| `message-version` | Contract version |
| `message-id` | Consumer deduplication identity |
| `correlation-id` | End-to-end request correlation |

Consumers should ignore unknown JSON fields and reject unsupported message versions explicitly.

## Routing Examples

```text
partner.transaction.#
```

Receives accepted and unverified transaction events.

```text
partner.transaction.accepted
```

Receives only accepted transaction events.

```text
partner.transaction.unverified
```

Receives only unverified transaction events.

## Local POC Configuration

The Mock project uses separate configuration sections and concrete options classes:

```json
{
  "RabbitMqConsumer": {
    "QueueName": "partner-transactions",
    "BindingPattern": "partner.transaction.#",
    "Enabled": true
  },
  "RabbitMqAuditConsumer": {
    "QueueName": "partner-transactions.audit",
    "BindingPattern": "partner.transaction.accepted",
    "Enabled": true
  }
}
```

The application runs one Mock service containing both consumers. The queues remain independent even though the hosted services share a process.

## Verification

The Mock observation endpoint is:

```text
GET /api/v1/mock/consumer-observations/{consumerName}
```

The local E2E suite verifies:

- One accepted publication reaches both independent queues.
- An unverified publication reaches the wildcard-bound primary queue only.
- An audit message that fails before acknowledgement is redelivered.

## Scope

This runbook documents the local POC. Azure Function hosting, broker networking, TLS, and cloud deployment are outside this scope.
