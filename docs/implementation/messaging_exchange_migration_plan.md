# Messaging Exchange Migration — Implementation Plan

Scope: migrate the publish path from the AMQP default exchange to a topic exchange so multiple independent consumers can be added without changing the API.

Design reference: [../architecture_design/messaging_topology_and_consumer_routing.md](../architecture_design/messaging_topology_and_consumer_routing.md)

Related guidance:

- [repo_architecture_rules.md](repo_architecture_rules.md)
- [shared_engineering_principles.md](shared_engineering_principles.md)
- [implementation_checklist.md](implementation_checklist.md)

---

## Locked decisions

- [x] Exchange type is `topic`, not `fanout`.
- [x] Exchange name is `partner.transactions`, durable.
- [x] Routing key format is `partner.transaction.<outcome>` where outcome is `accepted` or `unverified`.
- [x] The publisher declares the exchange only. Consumers own their queues, bindings, and DLQs.
- [x] `IMessagePublisher` in Core stays transport-agnostic; no exchange or routing-key parameters are added to it.
- [x] The existing `partner-transactions` queue stays functional throughout the migration via a compatibility binding.
- [x] Connection and channel reuse is included in this scope, because it touches the same adapter methods.
- [x] Publisher confirms remain mandatory; a missing confirm continues to fail the request.

---

## Phase 0 — Preconditions

- [x] Confirm the unused manual validator cleanup is already merged so this branch is not mixing unrelated changes (`eddfc13`).
- [x] Capture a baseline: run unit + integration suites and record pass counts (`35` unit tests and `11` integration tests passed).
- [x] Start `docker compose up -d rabbitmq` and confirm the management UI is reachable on `http://localhost:15672` (HTTP `200`).
- [x] Record the current broker topology (durable queue `partner-transactions`, default exchange binding, no custom exchange) as the rollback reference.

---

## Phase 1 — Options model

Target file: [../../src/TransactionValidation.Configuration/Options/RabbitMqOptions.cs](../../src/TransactionValidation.Configuration/Options/RabbitMqOptions.cs)

- [x] Add `ExchangeName` (default `partner.transactions`).
- [x] Add `ExchangeType` (default `topic`).
- [x] Add `RoutingKeyPrefix` (default `partner.transaction`).
- [x] Add `PublishConfirmTimeoutSeconds` (default `5`) to replace the hardcoded 5-second literal in the adapter.
- [x] Keep `QueueName` and `Durable` for now; `QueueName` becomes the compatibility-binding target, not the publish target.
- [x] Convert properties to `{ get; init; }`.

Config files to update with the new keys:

- [x] [../../src/TransactionValidation.Api/appsettings.json](../../src/TransactionValidation.Api/appsettings.json) — `RabbitMq` section
- [x] [../../src/TransactionValidation.Api/appsettings.Development.json](../../src/TransactionValidation.Api/appsettings.Development.json) — `RabbitMq` section
- [x] `.env.example` if broker settings are surfaced as environment variables

Acceptance: the app starts with the new keys absent and falls back to defaults without throwing.

---

## Phase 2 — Adapter contract and implementation

Target files:

- [../../src/TransactionValidation.Messaging/IRabbitMqClientAdapter.cs](../../src/TransactionValidation.Messaging/IRabbitMqClientAdapter.cs)
- [../../src/TransactionValidation.Messaging/RabbitMqClientAdapter.cs](../../src/TransactionValidation.Messaging/RabbitMqClientAdapter.cs)

### 2.1 Contract

- [ ] Add `DeclareExchangeAsync(string exchangeName, string exchangeType, bool durable, CancellationToken)`.
- [ ] Add `PublishPersistentWithConfirmAsync(string exchangeName, string routingKey, string payload, IReadOnlyDictionary<string, object> headers, CancellationToken)`.
- [ ] Keep `DeclareDurableQueueAsync` — the Mock consumer and tests still need queue declaration.
- [ ] Remove the old queue-based publish overload only after Phase 4 compiles.

### 2.2 Implementation

- [ ] Implement `DeclareExchangeAsync` using the reflection compat layer, mirroring the existing `QueueDeclareAsync` fallback ladder in [RabbitMqApiCompat.cs](../../src/TransactionValidation.Messaging/RabbitMqApiCompat.cs).
- [ ] Update the publish path to pass `exchangeName` and `routingKey` instead of `string.Empty` and the queue name.
- [ ] Set `mandatory: true` on publish so unroutable messages are returned rather than dropped.
- [ ] Apply `headers` to the basic-properties instance before publishing.
- [ ] Replace the hardcoded `TimeSpan.FromSeconds(5)` confirm timeout with the configured value.

### 2.3 Confirm-path correctness

These are existing defects in the confirm logic that this phase must fix, since the same lines are being edited.

- [ ] Check the return value of the `ConfirmSelect` / `ConfirmSelectAsync` invocation. If neither variant resolves, throw instead of continuing — otherwise confirm mode is silently never enabled.
- [ ] Replace the terminal `return true` with a thrown exception when no `WaitForConfirms` variant is found. Reporting success without a broker ack turns the publish into fire-and-forget on a client-version change.
- [ ] Replace `as bool? ?? true` with explicit handling: a non-boolean confirm result should fail, not default to confirmed.

Acceptance: a forced reflection miss produces a hard failure, not a false success.

---

## Phase 3 — Connection and channel reuse

Target file: [../../src/TransactionValidation.Messaging/RabbitMqClientAdapter.cs](../../src/TransactionValidation.Messaging/RabbitMqClientAdapter.cs)

The adapter currently opens and disposes a connection and channel on every declare and every publish.

- [ ] Hold a single long-lived connection in the adapter.
- [ ] Hold a channel guarded by a `SemaphoreSlim`, since an AMQP channel is not thread-safe.
- [ ] Add lazy initialization that creates the connection and channel on first use.
- [ ] Add reconnect handling: on a broker or channel fault, dispose and recreate on the next publish.
- [ ] Implement `IAsyncDisposable` and dispose the channel and connection on shutdown.
- [ ] Change the DI registration in [ServiceCollectionExtensions.cs](../../src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs) so the adapter singleton is disposed by the container.

Acceptance: publishing N messages opens one connection, verified in the RabbitMQ management UI connection count.

---

## Phase 4 — Routing key resolution

New file: `src/TransactionValidation.Messaging/IMessageRoutingKeyResolver.cs`

- [ ] Define `IMessageRoutingKeyResolver` with `string Resolve(TransactionEnvelope envelope)`.
- [ ] Implement `PartnerTransactionRoutingKeyResolver` returning `{prefix}.accepted` when `PartnerVerified` is true, otherwise `{prefix}.unverified`.
- [ ] Register the resolver as a singleton.

Constraint: the resolver lives in Messaging, not Core and not Api. Controllers must never construct routing keys.

Acceptance: unit tests cover both outcomes and confirm the configured prefix is honored.

---

## Phase 5 — Publisher

Target file: [../../src/TransactionValidation.Messaging/RabbitMqMessagePublisher.cs](../../src/TransactionValidation.Messaging/RabbitMqMessagePublisher.cs)

- [ ] Replace the `_queueName` field with `_exchangeName` and inject `IMessageRoutingKeyResolver`.
- [ ] Remove the per-publish `DeclareDurableQueueAsync` call.
- [ ] Build the header dictionary: `message-type`, `message-version`, `correlation-id`, `message-id`.
- [ ] Resolve the routing key, publish once, and keep the existing `ConflictException` on an unconfirmed publish.

Header values:

| Header | Source |
|---|---|
| `message-type` | Constant `PartnerTransactionAccepted` |
| `message-version` | Constant `1` |
| `correlation-id` | `envelope.CorrelationId` |
| `message-id` | `envelope.MessageId` |

Acceptance: `PublishAsync` performs exactly one broker publish and zero queue declarations.

---

## Phase 6 — Startup topology declaration

New file: `src/TransactionValidation.Messaging/RabbitMqTopologyInitializer.cs`

- [ ] Implement an `IHostedService` that calls `DeclareExchangeAsync` once at startup.
- [ ] Make failure non-fatal but logged as an error, so the API still starts when the broker is briefly unavailable; the first publish will retry declaration.
- [ ] Register it in [ServiceCollectionExtensions.cs](../../src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs).

Rationale: exchange declaration is idempotent and belongs at startup, not on the request path.

---

## Phase 7 — Compatibility binding for the existing consumer

The Mock consumer reads from `partner-transactions` and must not break.

Reference: [../../src/TransactionValidation.Mock/Services/RabbitMqNoOpConsumerService.cs](../../src/TransactionValidation.Mock/Services/RabbitMqNoOpConsumerService.cs)

- [ ] Add `ExchangeName` and `BindingPattern` (default `partner.transaction.#`) to [RabbitMqConsumerOptions.cs](../../src/TransactionValidation.Mock/Options/RabbitMqConsumerOptions.cs).
- [ ] Add a `QueueBindAsync` compat helper in [RabbitMqApiCompat.cs](../../src/TransactionValidation.Messaging/RabbitMqApiCompat.cs).
- [ ] In `DeclareQueueIfNeededAsync`, bind the queue to the exchange after declaring it.
- [ ] Update [../../src/TransactionValidation.Mock/appsettings.json](../../src/TransactionValidation.Mock/appsettings.json) and its Development variant.

Ordering constraint: this phase must be deployed **before** Phase 5 reaches an environment where the Mock consumer runs. If the publisher switches to the exchange first, messages route nowhere until the binding exists.

Acceptance: with both changes applied, the Mock consumer logs consumed messages exactly as before.

---

## Phase 8 — Unroutable message safety

- [ ] Declare an alternate exchange `partner.transactions.unrouted` and a bound queue `q.unrouted`.
- [ ] Set the `alternate-exchange` argument when declaring the main exchange.
- [ ] Log a warning when a basic-return is received for a mandatory publish.

Rationale: publisher confirms report broker acceptance, not delivery. A routing key matching no binding is confirmed and discarded. Without this phase, a misconfigured binding is invisible.

---

## Phase 9 — Tests

### 9.1 Unit

- [ ] `PartnerTransactionRoutingKeyResolver` — accepted, unverified, custom prefix.
- [ ] `RabbitMqMessagePublisher` — asserts exchange name, routing key, and headers passed to a mocked adapter.
- [ ] `RabbitMqMessagePublisher` — unconfirmed publish still throws `ConflictException`.
- [ ] Publisher no longer calls `DeclareDurableQueueAsync`.

Note: these assert on a mocked `IRabbitMqClientAdapter`, which is acceptable because the adapter boundary is the seam under test. Broker behavior itself is covered in 9.2.

### 9.2 Integration

- [ ] Publish and assert the message arrives on a queue bound with `partner.transaction.#`.
- [ ] Publish and assert a queue bound only to `partner.transaction.accepted` does not receive an unverified message.
- [ ] Assert two queues bound to the same routing key both receive the message — the core multi-consumer guarantee.
- [ ] Assert an unroutable publish lands in `q.unrouted`.
- [ ] Mark all with `[Trait("Category", "Integration")]`.

### 9.3 Regression

- [ ] Existing API host tests still pass unchanged.
- [ ] E2E smoke path in [../test/e2e_smoke_matrix.md](../test/e2e_smoke_matrix.md) still passes.

---

## Phase 10 — Documentation sync

- [ ] Update [../architecture_design/Architecture_design.md](../architecture_design/Architecture_design.md) messaging section to reference the exchange topology.
- [ ] Update the mermaid diagram in [../../README.md](../../README.md) so the publish step targets an exchange.
- [ ] Mark the legacy default-exchange section in [../architecture_design/messaging_topology_and_consumer_routing.md](../architecture_design/messaging_topology_and_consumer_routing.md) as retired once Phase 7 completes.
- [ ] Add a short "adding a new consumer" runbook: declare queue, bind pattern, add DLQ, deduplicate on `message-id`.

---

## Execution order

```mermaid
flowchart TD
    P0[Phase 0<br/>baseline] --> P1[Phase 1<br/>options]
    P1 --> P2[Phase 2<br/>adapter + confirm fixes]
    P2 --> P3[Phase 3<br/>connection reuse]
    P2 --> P4[Phase 4<br/>routing resolver]
    P3 --> P5[Phase 5<br/>publisher]
    P4 --> P5
    P5 --> P6[Phase 6<br/>startup declare]
    P6 --> P7[Phase 7<br/>compat binding]
    P7 --> P8[Phase 8<br/>unroutable safety]
    P8 --> P9[Phase 9<br/>tests]
    P9 --> P10[Phase 10<br/>docs]
```

Phases 3 and 4 are independent and can be done in either order.

---

## Deployment sequence

Because the publisher and consumer are separate processes, ordering matters in a running environment.

| Step | Deploy | Risk if skipped |
|---|---|---|
| 1 | Exchange declaration (Phase 6) | None; declaring an unused exchange is inert |
| 2 | Consumer binding (Phase 7) | Messages route nowhere after step 3 |
| 3 | Publisher switch (Phase 5) | — |
| 4 | Verify consumer drain | Silent message loss goes unnoticed |
| 5 | Remove legacy config | — |

Rollback: revert the publisher to default-exchange publishing. The queue and its messages are untouched, so no data is lost.

---

## Definition of done

- [ ] The API publishes one message to `partner.transactions` per accepted transaction.
- [ ] Two queues bound to the same routing key both receive it.
- [ ] The Mock consumer works without code changes beyond the binding.
- [ ] One broker connection is used across many publishes.
- [ ] A missing confirm still returns a failure to the caller.
- [ ] A reflection miss on confirm APIs fails loudly instead of silently succeeding.
- [ ] Unroutable messages are captured and logged.
- [ ] Unit and integration suites are green.
- [ ] No exchange or routing-key concepts appear in Core or Api.

---

## Out of scope

- Per-consumer publisher implementations or a publisher-side list of destinations. Both reintroduce producer-to-consumer coupling.
- Replacing the in-memory idempotency store with Redis.
- Consumer-side retry/backoff policies beyond DLQ declaration.
- Schema registry or contract-testing tooling.
