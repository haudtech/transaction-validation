# Distributed State

## Purpose and Scope

This document defines the implemented idempotency-state storage model for TransactionValidation. It covers runtime store selection, TTL-bounded request reservation, duplicate and conflict detection, accepted-response caching, key protection, failure release, Redis reachability, and single-replica versus multi-replica behavior.

It does not define the complete HTTP orchestration, partner-verification resilience, Redis infrastructure provisioning, or general-purpose application caching.

## Behavior Inventory

- Configuration-based idempotency-store selection
- TTL clamping and expiry
- First-request reservation
- Duplicate and conflicting-payload detection
- Accepted-response caching and replay support
- Failed-request reservation release
- Raw-key hashing before storage
- Redis reachability health reporting

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Store abstraction | .NET interface contract | Broker-independent acquire/read/store/release operations | `IIdempotencyStore` | Controller and store tests |
| Local state | `ConcurrentDictionary` | Thread-safe process-local entries with lazy and periodic expiry cleanup | `InMemoryIdempotencyStore` | In-memory store unit tests, including concurrency |
| Distributed state | StackExchange.Redis | Atomic `SET NX` fingerprint reservation with TTL | `RedisIdempotencyStore` | Redis store unit tests and Redis-backed E2E replay behavior |
| Runtime selection | Microsoft dependency injection | Redis when a connection string exists; in-memory fallback otherwise | `IdempotencyStoreRegistration` | Registration unit tests |
| Key protection | SHA-256 | Hash logical idempotency keys before using them as local or Redis keys | Both store implementations | Store unit tests and source review |
| Response replay state | JSON serialization | Store accepted message/correlation/status metadata under the same TTL policy | Both store implementations | Store and controller replay tests |
| State health | ASP.NET Core health checks and Redis ping | Probe Redis only when the distributed store is configured | `RedisHealthCheck` | Health-check unit tests and API host health test |

## Definitions and Semantics

### Logical key and request fingerprint

The controller supplies two distinct values to the store:

- **Logical idempotency key** - partner scope combined with the caller's `Idempotency-Key` header, or with `transactionReference` when the header is absent.
- **Request fingerprint** - SHA-256 digest of canonical request fields used to distinguish an identical retry from reuse of the same logical key with different content.

Each store hashes the logical key before using it as an internal dictionary or Redis key. The fingerprint is stored as the comparison value.

### Acquisition outcomes

| Outcome | Meaning |
|---|---|
| `Acquired` | No active reservation exists, so this request owns the processing slot. |
| `Duplicate` | An active reservation exists with the same request fingerprint. |
| `KeyReusedWithDifferentPayload` | An active reservation exists with a different request fingerprint. |

The store reports state; the API workflow decides which HTTP response or subsequent action follows.

### TTL policy

`Idempotency:WindowMinutes` defaults to 15 and is clamped to the inclusive range of 10 through 15 minutes by `IdempotencyStoreRegistration.GetWindow`.

The in-memory implementation evaluates expiry against the supplied UTC timestamp, removes expired entries lazily on access, and periodically scans for expired entries. The Redis implementation delegates expiration to Redis through key TTL values.

### Accepted-response cache

After successful publication, the store records:

- message identifier
- correlation identifier
- accepted status

A duplicate request with the same fingerprint can read this value so the API returns the original accepted result without repeating partner verification or publication.

### Failure release

When processing fails after acquisition, the API releases the logical key. Both stores remove reservation and cached-response state so a later valid retry can attempt processing again.

### Store selection

When `Redis:ConnectionString` is blank, the API registers `InMemoryIdempotencyStore`. When it is configured, the API creates an `IConnectionMultiplexer` and registers `RedisIdempotencyStore`.

The in-memory store is suitable only when one API process owns all relevant retries. Redis is the shared-state mode for multiple replicas.

## Implementation Pattern Rules

### Pattern: Store behind one workflow contract

- **Applies to** - idempotency persistence; cross-cutting API state.
- **Intent** - preserve one workflow regardless of whether state is local or distributed.
- **Rule** - idempotency stores MUST implement `IIdempotencyStore`. Controllers MUST depend on that contract and MUST NOT branch on Redis or in-memory types.
- **Approved code shape**:

```csharp
public interface IIdempotencyStore
{
    IdempotencyAcquireResult TryAcquire(
        string key,
        string requestFingerprint,
        DateTimeOffset nowUtc);

    bool TryGetCachedResponse(
        string key,
        string requestFingerprint,
        DateTimeOffset nowUtc,
        out IdempotencyCachedResponse response);

    void StoreCachedResponse(
        string key,
        string requestFingerprint,
        DateTimeOffset nowUtc,
        IdempotencyCachedResponse response);

    void Release(string key);
}
```

- **Avoid** - Redis-specific controller branches, transport types in the interface, or different acquire outcomes per implementation.
- **Implementation references** - `IIdempotencyStore`, `InMemoryIdempotencyStore`, `RedisIdempotencyStore`.
- **Verification** - controller tests and both store test suites.

### Pattern: Select state implementation at composition time

- **Applies to** - API dependency registration; environment-specific.
- **Intent** - use local state by default and shared state when a Redis connection is supplied.
- **Rule** - store selection MUST occur once during API composition. A non-empty Redis connection MUST select the Redis implementation; an empty value MUST select the in-memory fallback.
- **Approved code shape**:

```csharp
if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
{
    services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
    return;
}

services.AddSingleton<IConnectionMultiplexer>(CreateRedisConnection);
services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
```

- **Avoid** - selecting a store per request, silently using independent in-memory stores across replicas, or registering both implementations as the active contract.
- **Implementation references** - `IdempotencyStoreRegistration`; `RedisOptions`.
- **Verification** - `IdempotencyStoreRegistrationTests`.

### Pattern: Atomic first-writer reservation

- **Applies to** - acquisition of an idempotency key; store-specific implementations.
- **Intent** - allow one request to acquire a key while classifying concurrent retries by fingerprint.
- **Rule** - acquisition MUST be atomic within the selected store's scope. Redis acquisition MUST use create-if-absent semantics with a TTL. In-memory acquisition MUST use atomic concurrent-dictionary operations.
- **Approved code shape**:

```csharp
if (database.StringSet(fingerprintKey, fingerprint, ttl, When.NotExists))
{
    return IdempotencyAcquireResult.Acquired;
}

var existing = database.StringGet(fingerprintKey);
return existing == fingerprint
    ? IdempotencyAcquireResult.Duplicate
    : IdempotencyAcquireResult.KeyReusedWithDifferentPayload;
```

- **Avoid** - non-atomic read-then-create reservation, unbounded keys, or treating different payloads as safe duplicates.
- **Implementation references** - `RedisIdempotencyStore.TryAcquire`; `InMemoryIdempotencyStore.TryAcquire`.
- **Verification** - Redis acquisition tests and concurrent in-memory acquisition test.

### Pattern: Bounded accepted-response replay

- **Applies to** - successful request completion and duplicate replay.
- **Intent** - return the original acceptance metadata without repeating external side effects.
- **Rule** - accepted-response state MUST be associated with the matching fingerprint and bounded by the configured TTL. Lookup MUST fail when the fingerprint differs, state is absent, or state has expired.
- **Approved code shape**:

```csharp
store.StoreCachedResponse(key, fingerprint, nowUtc, acceptedResponse);

if (store.TryGetCachedResponse(key, fingerprint, nowUtc, out var cached))
{
    return Accepted(cached);
}
```

- **Avoid** - replaying a response for a different fingerprint, caching failed outcomes as accepted, or storing unbounded response state.
- **Implementation references** - both store implementations; `PartnerTransactionsController` duplicate branch.
- **Verification** - store cache tests, controller duplicate tests, and API idempotency host test.

### Pattern: Release reservation on failed processing

- **Applies to** - failures after successful acquisition; API workflow and stores.
- **Intent** - prevent a transient verification or publication failure from blocking a later retry for the full TTL.
- **Rule** - the workflow MUST release the acquired key when downstream processing throws before acceptance. Release MUST remove both reservation and cached-response state for that logical key.
- **Approved code shape**:

```csharp
try
{
    await ProcessAndPublishAsync(cancellationToken);
    store.StoreCachedResponse(key, fingerprint, nowUtc, acceptedResponse);
}
catch
{
    store.Release(key);
    throw;
}
```

- **Avoid** - retaining failed reservations, returning acceptance before caching, or swallowing the original processing exception.
- **Implementation references** - `PartnerTransactionsController.CreateAsync`; both `Release` implementations.
- **Verification** - controller verification/publish failure tests and store release tests.

### Pattern: Hash external key material before storage

- **Applies to** - local and Redis key construction; store-specific.
- **Intent** - avoid storing the caller-provided logical idempotency material directly as a cache key.
- **Rule** - store implementations MUST normalize and SHA-256 hash the logical key before dictionary or Redis key construction. Logs MUST NOT include raw idempotency keys.
- **Approved code shape**:

```csharp
var bytes = Encoding.UTF8.GetBytes(key.Trim());
var encodedKey = Convert.ToHexString(SHA256.HashData(bytes));
```

- **Avoid** - raw caller keys in Redis names, dictionaries, or structured logs.
- **Implementation references** - key encoding helpers in both stores; logging contract tests.
- **Verification** - source review, store tests, and structured-property allowlist tests.

### Pattern: Conditional distributed-state health check

- **Applies to** - API health reporting; environment-specific.
- **Intent** - report Redis reachability only when Redis is part of the active runtime.
- **Rule** - the health check MUST return a healthy in-memory-mode result when no multiplexer is registered. When Redis is configured, it MUST ping the selected database and report failures as unhealthy.
- **Approved code shape**:

```csharp
if (connectionMultiplexer is null)
{
    return HealthCheckResult.Healthy("Redis not configured; using in-memory idempotency store.");
}

await connectionMultiplexer.GetDatabase().PingAsync();
return HealthCheckResult.Healthy("Redis is reachable.");
```

- **Avoid** - failing local in-memory mode because Redis is absent or reporting configured Redis as healthy without probing it.
- **Implementation references** - `RedisHealthCheck`; API health-check registration.
- **Verification** - `HealthChecksTests` and API health host test.

## Configuration Contract

| Section/key | Default or rule | Purpose |
|---|---|---|
| `Idempotency:WindowMinutes` | Default 15; clamped to 10-15 | Controls reservation and cached-response lifetime. |
| `Redis:ConnectionString` | Empty selects in-memory; non-empty selects Redis | Chooses process-local or distributed state. |

The Redis connection string is sensitive configuration and must be supplied through the appropriate local secret or deployed secret path rather than committed to production settings.

## Source Ownership

| Project or abstraction | Responsibility |
|---|---|
| `TransactionValidation.Api` | Store contract, both implementations, registration, API orchestration, and Redis health check. |
| `TransactionValidation.Configuration` | Typed idempotency and Redis options. |
| `TransactionValidation.Core` | Structured idempotency event definitions. |
| `TransactionValidation.Tests` | Store semantics, registration, health, controller, and host replay verification. |

## Verification Surface

| Verification | Proves |
|---|---|
| `InMemoryIdempotencyStoreTests` | TTL, duplicate/conflict outcomes, cached responses, release, cleanup, validation, and concurrent acquisition. |
| `RedisIdempotencyStoreTests` | Redis reservation, duplicate/conflict outcomes, response storage, and release. |
| `IdempotencyStoreRegistrationTests` | TTL clamping and configuration-based implementation selection. |
| `PartnerTransactionsControllerTests` | Replay without side effects, conflicts, successful caching, and release after failure. |
| `ApiIdempotencyHostTests` | Cached accepted replay through the ASP.NET Core host. |
| `HealthChecksTests` | In-memory no-op health, successful Redis ping, and Redis failure reporting. |

## Operational Boundaries

- In-memory state is process-local and cannot coordinate duplicate requests across API replicas.
- Redis acquisition atomically reserves the fingerprint key, but fingerprint refresh and accepted-response storage are separate Redis operations; the implementation does not claim a multi-key transaction or exactly-once processing.
- Redis calls in the current store contract are synchronous even though Redis health probing is asynchronous.
- State is TTL-bounded and is not a permanent transaction ledger or audit store.
- Releasing a key permits a later retry; it does not roll back effects already completed outside the store.
- Downstream consumers must remain idempotent because broker redelivery is independent of API request deduplication.

## Related Supporting Documents

- [Runtime and API](../runtime-and-api/README.md)
- [API idempotency flow and semantics](../architecture_design/api_idempotency_flow_and_semantics.md)
- [Architecture design overview](../architecture_design/Architecture_design.md)
