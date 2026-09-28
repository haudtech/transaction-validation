# Reliability

## Purpose and Scope

This document defines the implemented reliability behavior for partner verification and request retry safety. It covers the centralized HTTP resilience pipeline, timeout guardrails, retry and circuit-breaker configuration, upstream outcome translation, caller cancellation, and release of idempotency reservations after failed processing.

It does not define Redis storage mechanics, broker delivery guarantees, cloud availability architecture, or unimplemented fallback strategies.

## Behavior Inventory

- Centralized partner-verification HTTP resilience
- Bounded retry attempts
- Per-attempt and total timeout budgets
- Circuit-breaker configuration
- HTTP and transport outcome translation
- Caller cancellation propagation
- Idempotency release after verification or publication failure
- Deterministic Mock timeout control for tests

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Resilient HTTP client | `Microsoft.Extensions.Http.Resilience` | Typed `HttpClient` with `AddStandardResilienceHandler` | `ServiceCollectionExtensions` | Retry and timeout-guardrail tests |
| Retry budget | Standard resilience retry strategy | Configurable maximum retries without client-side loops | `PartnerVerificationOptions`, DI registration | `PartnerVerifierResilienceRetryTests` |
| Timeout budgets | Standard attempt/total timeout strategies plus `HttpClient.Timeout` | Effective-value guardrails ensure total timeout exceeds attempt timeout | Shared DI registration | `PartnerVerificationTimeoutGuardrailTests` |
| Circuit breaking | Standard resilience circuit breaker | Configurable minimum throughput and break duration | Shared DI registration | Registration/source verification |
| Outcome translation | `HttpClient` and domain exceptions | Map terminal HTTP/transport outcomes to stable exception categories | `PartnerVerifierClient` | Verifier client tests |
| API failure responses | ASP.NET Core `IExceptionHandler` | Map established domain exceptions to ProblemDetails | `ApiExceptionHandler` | Handler and API host tests |
| Retry-safe request state | Idempotency store contract | Release acquired reservation when processing throws before acceptance | `PartnerTransactionsController` | Controller failure tests |
| Timeout simulation | ASP.NET Core Mock endpoint | Random 30% HTTP `408` with optional deterministic query override | `MockPartnerVerificationController` | Mock integration tests |

## Definitions and Semantics

### Typed resilience pipeline

`IPartnerVerifier` resolves to `PartnerVerifierClient` through a typed `HttpClient`. The resilience policy is attached during dependency registration, not implemented in the client method.

The configured standard handler owns retry, attempt timeout, total timeout, and circuit-breaking behavior. `PartnerVerifierClient` owns request construction and translation of terminal HTTP or transport outcomes.

### Retry count

`PartnerVerification:RetryCount` sets `Retry.MaxRetryAttempts`. The total number of sends can therefore be the initial attempt plus the configured retry count when the standard strategy classifies each previous outcome as retryable and the total timeout budget remains available.

The application does not configure a custom retry delay or backoff sequence; other retry behavior comes from the library's standard resilience handler.

### Effective timeout values

The registration derives timeout values as follows:

1. `TimeoutSeconds` is clamped to at least one second and retained as the legacy fallback.
2. A positive `AttemptTimeoutSeconds` is used; otherwise the legacy fallback is used.
3. A positive `TotalRequestTimeoutSeconds` is used; otherwise total timeout is computed as `max(attempt + 1, attempt * (retryCount + 1))`.
4. If the total is not greater than the attempt timeout, total timeout is raised to `attempt + 1` second.
5. `HttpClient.Timeout` is set to the same effective total timeout used by the resilience registration.

### Circuit breaker

The standard circuit breaker uses:

- minimum throughput of `max(2, CircuitBreakerFailures)`
- break duration from `CircuitBreakerDurationSeconds`

The application does not configure the complete sampling and failure-ratio model directly; unspecified behavior remains the standard handler's library behavior.

### Terminal outcome translation

| Terminal outcome observed by `PartnerVerifierClient` | Domain result |
|---|---|
| Any successful HTTP response | `true` |
| HTTP `404` | `NotFoundException` |
| HTTP `408` | `UpstreamTimeoutException` |
| HTTP `503` or any other `5xx` | `UpstreamServiceUnavailableException` |
| Other non-success HTTP status | `UpstreamServiceUnavailableException` containing the unexpected status |
| `TaskCanceledException` when caller cancellation was not requested | `UpstreamTimeoutException` |
| `HttpRequestException` | `UpstreamServiceUnavailableException` |
| Caller-requested cancellation | Cancellation propagates without translation |

The API exception handler maps `NotFoundException`, `UpstreamTimeoutException`, and `UpstreamServiceUnavailableException` to HTTP `404`, `408`, and `503` ProblemDetails respectively.

Policy-generated exception types that are not `TaskCanceledException` or `HttpRequestException` are not explicitly translated by the current client. They flow to the generic exception handler and can produce HTTP `500`. This includes the current unverified boundary for circuit-open and Polly timeout rejection exceptions.

### Retry-safe idempotency behavior

The controller acquires idempotency state before partner verification. If verification or publication throws, the controller logs the failure, releases the idempotency key, and rethrows. A later retry can therefore reacquire the key rather than remaining blocked for the full TTL.

### Mock timeout behavior

The Mock endpoint returns HTTP `408` for approximately 30% of calls when no override is supplied. Tests can provide `forceTimeout=true` or `forceTimeout=false` through `IPartnerVerifier.VerifyAsync` to select deterministic behavior.

## Implementation Pattern Rules

### Pattern: Centralize resilience in the typed HTTP pipeline

- **Applies to** - partner-verification HTTP calls; cross-project Configuration/Integration boundary.
- **Intent** - provide one consistent policy layer and keep the client focused on protocol translation.
- **Rule** - partner-verification retry, timeout, and circuit-breaker policies MUST be configured through the typed `HttpClient` registration. `PartnerVerifierClient` MUST NOT implement manual retry loops.
- **Approved code shape**:

```csharp
services.AddHttpClient<IPartnerVerifier, PartnerVerifierClient>(client =>
{
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = effectiveTotalTimeout;
})
.AddStandardResilienceHandler(pipeline =>
{
    pipeline.Retry.MaxRetryAttempts = options.RetryCount;
    pipeline.AttemptTimeout.Timeout = effectiveAttemptTimeout;
    pipeline.TotalRequestTimeout.Timeout = effectiveTotalTimeout;
    pipeline.CircuitBreaker.MinimumThroughput = minimumThroughput;
    pipeline.CircuitBreaker.BreakDuration = breakDuration;
});
```

- **Avoid** - `for`/`while` retry loops in the client, stacked custom retry handlers, or environment-specific policy code in controllers.
- **Implementation references** - `ServiceCollectionExtensions.AddTransactionValidationCommonServices`; `PartnerVerifierClient`.
- **Verification** - `PartnerVerifierResilienceRetryTests` and timeout-guardrail tests.

### Pattern: Enforce nested timeout guardrails

- **Applies to** - partner-verification timeout configuration; cross-cutting.
- **Intent** - ensure one attempt has a bounded duration and the complete operation has a larger bounded budget.
- **Rule** - effective attempt timeout MUST be positive. Effective total timeout MUST be greater than attempt timeout. Missing values MUST use the implemented fallback calculation rather than disabling timeout protection.
- **Approved code shape**:

```csharp
var attempt = configuredAttempt > 0
    ? configuredAttempt
    : Math.Max(1, legacyTimeout);

var total = configuredTotal > 0
    ? configuredTotal
    : Math.Max(attempt + 1, attempt * (retryCount + 1));

if (total <= attempt)
{
    total = attempt + 1;
}
```

- **Avoid** - non-positive attempt budgets, total timeout equal to or below attempt timeout, or unrelated timeout calculations in the client.
- **Implementation references** - timeout calculation in `ServiceCollectionExtensions`; `PartnerVerificationOptions`.
- **Verification** - `PartnerVerificationTimeoutGuardrailTests`.

### Pattern: Translate terminal dependency outcomes at the adapter

- **Applies to** - completed partner-verification HTTP calls and transport failures; Integration-specific.
- **Intent** - prevent HTTP-client details from leaking into API orchestration.
- **Rule** - the integration adapter MUST translate known terminal status and transport outcomes into the established Core exception categories. Controllers MUST NOT inspect partner HTTP status codes.
- **Approved code shape**:

```csharp
if (response.IsSuccessStatusCode)
{
    return true;
}

throw response.StatusCode switch
{
    HttpStatusCode.NotFound => new NotFoundException(message),
    HttpStatusCode.RequestTimeout => new UpstreamTimeoutException(message),
    >= HttpStatusCode.InternalServerError => new UpstreamServiceUnavailableException(message),
    _ => new UpstreamServiceUnavailableException(unexpectedStatusMessage)
};
```

- **Avoid** - returning `false` for ambiguous failures, exposing `HttpResponseMessage` to the controller, or mapping all failures to one generic category.
- **Implementation references** - `PartnerVerifierClient`; Core upstream exception types; `ApiExceptionHandler`.
- **Verification** - `PartnerVerifierClientTests`, `ApiExceptionHandlerTests`, and API exception host tests.

### Pattern: Preserve caller cancellation

- **Applies to** - cancellation during partner verification; Integration-specific.
- **Intent** - distinguish caller or shutdown cancellation from a dependency timeout.
- **Rule** - the caller's cancellation token MUST flow to `HttpClient`. `TaskCanceledException` MUST be translated to `UpstreamTimeoutException` only when the caller token was not canceled.
- **Approved code shape**:

```csharp
try
{
    return await httpClient.GetAsync(path, cancellationToken);
}
catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    throw new UpstreamTimeoutException("Partner verification request timed out.");
}
```

- **Avoid** - swallowing caller cancellation, translating every cancellation to HTTP `408`, or creating an unrelated token inside the client.
- **Implementation references** - `PartnerVerifierClient.VerifyAsync`.
- **Verification** - caller-cancellation and request-timeout tests in `PartnerVerifierClientTests`.

### Pattern: Release request reservation after downstream failure

- **Applies to** - partner verification and publication failures after idempotency acquisition; API-specific.
- **Intent** - allow a valid retry after a failed attempt while preserving accepted-response replay after success.
- **Rule** - the API workflow MUST release the acquired idempotency key when downstream processing throws before acceptance, then rethrow the original exception.
- **Approved code shape**:

```csharp
try
{
    await verifier.VerifyAsync(partnerId, cancellationToken);
    await publisher.PublishAsync(envelope, cancellationToken);
    store.StoreCachedResponse(key, fingerprint, nowUtc, acceptedResponse);
}
catch
{
    store.Release(key);
    throw;
}
```

- **Avoid** - caching failed outcomes as accepted, retaining failed reservations until expiry, or swallowing the original failure.
- **Implementation references** - `PartnerTransactionsController.CreateAsync`; `IIdempotencyStore.Release`.
- **Verification** - controller verification-failure and publication-failure tests.

### Pattern: Deterministic failure control for tests

- **Applies to** - Mock partner verification; test-support behavior.
- **Intent** - retain realistic random timeout behavior while allowing deterministic resilience and API tests.
- **Rule** - the Mock MAY use random HTTP `408` behavior when no override is supplied. Tests SHOULD use the explicit timeout override when deterministic behavior is required.
- **Approved code shape**:

```csharp
var shouldTimeout = forceTimeout ?? (Random.Shared.NextDouble() < TimeoutRate);
return shouldTimeout
    ? StatusCode(StatusCodes.Status408RequestTimeout, timeoutPayload)
    : Ok(verifiedPayload);
```

- **Avoid** - relying on random outcomes in deterministic unit/host tests or throwing process-level timeout exceptions from the Mock controller.
- **Implementation references** - `MockPartnerVerificationController`; `IPartnerVerifier.VerifyAsync` test-support parameter.
- **Verification** - `MockPartnerVerificationControllerTests` and verifier request-path tests.

## Configuration Contract

| Section/key | Default | Implemented effect |
|---|---:|---|
| `PartnerVerification:BaseUrl` | `http://localhost:5002/` | Base address for the typed partner-verification client. |
| `PartnerVerification:RetryCount` | `3` | Maximum retry attempts configured on the standard retry strategy. |
| `PartnerVerification:TimeoutSeconds` | `10` | Legacy positive fallback when attempt timeout is not configured. |
| `PartnerVerification:AttemptTimeoutSeconds` | `10` | Per-attempt timeout when positive. |
| `PartnerVerification:TotalRequestTimeoutSeconds` | `30` | Total operation timeout when positive and greater than attempt timeout. |
| `PartnerVerification:CircuitBreakerFailures` | `5` | Input to minimum throughput, clamped to at least two. |
| `PartnerVerification:CircuitBreakerDurationSeconds` | `30` | Circuit break duration. |

## Source Ownership

| Project or abstraction | Responsibility |
|---|---|
| `TransactionValidation.Core` | `IPartnerVerifier` contract and stable exception categories. |
| `TransactionValidation.Configuration` | Options, typed-client registration, timeout guardrails, resilience pipeline, and API exception mapping. |
| `TransactionValidation.Integration` | Request construction, terminal outcome translation, cancellation distinction, and dependency logging. |
| `TransactionValidation.Api` | Workflow ordering and idempotency release when verification or publication fails. |
| `TransactionValidation.Mock` | Simulated partner endpoint and deterministic timeout override. |
| `TransactionValidation.Tests` | Retry, timeout, cancellation, status mapping, host response, and release verification. |

## Verification Surface

| Verification | Proves |
|---|---|
| `PartnerVerifierResilienceRetryTests` | Retryable `503` outcomes are retried and retry exhaustion ends in service-unavailable translation. |
| `PartnerVerificationTimeoutGuardrailTests` | Derived, corrected, and explicitly configured total timeout values. |
| `PartnerVerifierClientTests` | Request construction, status mapping, transport mapping, caller cancellation, and timeout distinction. |
| `ApiExceptionHandlerTests` | Established upstream exceptions map to `408` and `503` ProblemDetails. |
| API exception host tests | Mapped exceptions flow through the real host exception pipeline. |
| Controller failure tests | Idempotency reservations are released after verification or publication failure. |
| Mock integration tests | Deterministic `200`/`408` behavior and approximate random timeout rate. |

## Operational Boundaries

- The application configures the standard resilience handler rather than defining every retry predicate, delay, sampling window, or failure ratio itself.
- Retry tests explicitly prove `503` retry behavior; they do not prove every standard-handler transient classification.
- Circuit-breaker settings are registered, but no focused test currently opens the circuit and verifies its terminal API response.
- Policy-generated timeout or circuit-open exceptions outside the client's explicit catches can reach the generic HTTP `500` mapping.
- No fallback response, cached partner result, bulkhead, hedging policy, or persistent retry queue is implemented.
- API idempotency reduces duplicate request processing but does not provide exactly-once delivery or replace consumer idempotency.

## Related Supporting Documents

- [Runtime and API](../runtime-and-api/README.md)
- [Validation and security](../validation-and-security/README.md)
- [Distributed state](../distributed-state/README.md)
- [API idempotency flow and semantics](../architecture_design/api_idempotency_flow_and_semantics.md)
