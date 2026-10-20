# Validation and Security

## Purpose and Scope

This document defines the implemented request-validation and partner-facing security boundaries for TransactionValidation. It covers transaction validation, API-key enforcement, correlation-header validation, middleware ordering, and HTTP error representation.

It does not define endpoint orchestration, idempotency behavior, external-service resilience, Azure workload identity, or secret-provisioning procedures.

## Behavior Inventory

- Partner transaction request validation
- API-key request gating
- Health-probe authentication exemption
- Correlation identifier validation and generation
- Domain and upstream exception translation
- Middleware-owned rejection responses

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Request validation | FluentValidation | Explicit asynchronous validation before workflow processing | `PartnerTransactionRequestValidator`, `PartnerTransactionsController` | Validator and controller unit tests; API exception host tests |
| API-key gating | ASP.NET Core middleware and options | Fail-closed header comparison with configurable enablement/header name | `ApiKeyMiddleware`, `ApiKeyOptions` | Middleware unit tests and API security host tests |
| Health-probe access | ASP.NET Core path matching | Bypass partner authentication for `/healthz` | `ApiKeyMiddleware` | Middleware and startup host tests |
| Correlation validation | ASP.NET Core middleware and `Activity` | Trim caller value, reject control characters/oversize values, otherwise generate a fallback | `CorrelationContextMiddleware` | Correlation middleware unit tests |
| Exception responses | ASP.NET Core `IExceptionHandler` and RFC 7807 | Map known exception categories to stable `ProblemDetails` status responses | `ApiExceptionHandler` | Exception-handler unit tests and API exception host tests |
| Middleware rejections | ASP.NET Core JSON response writing | Short-circuit invalid API keys and correlation headers before controller execution | API-key and correlation middleware | Middleware unit tests and API security host tests |

## Definitions and Semantics

### Request contract

A partner transaction request contains:

| Field | Validation semantics |
|---|---|
| `partnerId` | Required and non-empty. |
| `transactionReference` | Required and non-empty. |
| `amount` | Must be greater than zero. |
| `currency` | Required; only the system-supported ISO-4217 codes `USD` and `EUR` are accepted, case-insensitively. The supported set is represented by `TransactionValidation.Core.Enums.CurrencyCode` and parsed by `TransactionValidation.Core.Parsers.CurrencyCodeParser`. |
| `timestamp` | Required and must not be the default `DateTime` value. |

The controller invokes the validator before idempotency acquisition, partner verification, or message publication. Validation failures become `BadRequestException` and are translated by the exception handler into HTTP `400` ProblemDetails.

### API-key boundary

API-key enforcement is configurable through the `Security` section. When enabled, the middleware reads the configured header name and compares its complete value with the configured key using ordinal comparison. A missing, blank, or different value returns HTTP `401` and stops the pipeline.

The `/healthz` path bypasses API-key enforcement so infrastructure probes do not require partner credentials. When API-key protection is disabled, the middleware forwards the request unchanged.

### Correlation boundary

The middleware accepts an optional `Correlation-Id` header. A supplied value is trimmed and accepted when it is no longer than 128 characters and contains no control characters. Invalid values return HTTP `400` and stop the pipeline.

When the header is absent, the middleware uses `HttpContext.TraceIdentifier`; if that is blank, it generates a compact GUID. The value is stored in `HttpContext.Items`, added to the Serilog request scope, and attached to the current `Activity` as `app.correlation_id`.

### Error response boundaries

| Source | Status | Representation |
|---|---|---|
| Invalid transaction request or explicit bad request | `400` | RFC 7807 `ProblemDetails` through `ApiExceptionHandler` |
| Invalid correlation header | `400` | Middleware-owned JSON `{ "error": "Correlation-Id header is invalid." }` |
| Missing or invalid API key | `401` | Middleware-owned JSON `{ "error": "Unauthorized" }` |
| `UnauthorizedAccessException` | `401` | RFC 7807 `ProblemDetails` |
| Partner not found | `404` | RFC 7807 `ProblemDetails` |
| Upstream timeout | `408` | RFC 7807 `ProblemDetails` |
| Idempotency or publish conflict | `409` | RFC 7807 `ProblemDetails` |
| Upstream unavailable | `503` | RFC 7807 `ProblemDetails` |
| Unhandled exception | `500` | RFC 7807 `ProblemDetails` without internal exception detail |

Middleware responses and exception-handler responses intentionally have different shapes in the current implementation. Documentation and clients must not assume that every `400` or `401` response is ProblemDetails.

## Implementation Pattern Rules

### Governance rule traceability

These rule IDs identify applicable coding requirements; the linked governance catalog owns the normative text and reusable C# examples. Each approved-pattern link points to a domain-specific implementation/code example showing how this feature applies the rule.

| Rule | Applicability and implementation | Approved feature pattern/code evidence | Verification evidence | Exception |
|---|---|---|---|---|
| [CS-CFG-01 — Bind and validate configuration at the composition boundary](../../governance/coding/coding_standards.md) | Applies to API-key behavior driven by bound options and shared host registration. | [Middleware-owned API-key gate](#pattern-middleware-owned-api-key-gate). | `ApiKeyMiddlewareTests`; API security host tests. | N/A |
| [CS-FLOW-01 — Validate before side effects and make state transitions explicit](../../governance/coding/coding_standards.md) | Applies to partner requests; validation runs before idempotency acquisition, verification, or publication. | [Validate before side effects](#pattern-validate-before-side-effects). | `PartnerTransactionRequestValidatorTests`; `PartnerTransactionsControllerTests`. | N/A |
| [CS-VAL-01 — Parse finite external values into typed domain values](../../governance/coding/coding_standards.md) | Applies to currency input; the wire value remains a string, while the Core parser accepts only supported enum values and normalizes surrounding whitespace/case. | [Request contract](#request-contract); `CurrencyCodeParser` and `CurrencyCode`. | `PartnerTransactionRequestValidatorTests` cover supported, lowercase, malformed, and unsupported currency values. | N/A |
| [CS-OBS-01 — Propagate context while minimizing sensitive telemetry](../../governance/coding/coding_standards.md) | Applies to correlation ingress and downstream propagation; raw idempotency keys are not logged. | [Validate correlation at the request boundary](#pattern-validate-correlation-at-the-request-boundary). | `CorrelationContextMiddlewareTests`; controller correlation tests. | N/A |
| [CS-HTTP-01 — Normalize ASP.NET Core errors with ProblemDetails](../../governance/coding/coding_standards.md) | Applies to API exception mapping; known exceptions preserve their message as `Detail`, while unknown failures use the generic safe detail. | [Central exception-to-HTTP translation](#pattern-central-exception-to-http-translation). | `ApiExceptionHandlerTests`; `ApiExceptionMappingHostTests` verify the HTTP ProblemDetails contract. | N/A |
| [CS-TEST-01 — Test observable behavior at the owning boundary](../../governance/coding/coding_standards.md) | Applies to middleware and exception behavior. | [Real-host boundary integration](../testing-and-quality/README.md#pattern-exercise-the-real-host-for-boundary-integration). | Middleware tests; API security and exception host tests. | N/A |

### Pattern: Validate before side effects

- **Applies to** - partner transaction requests; API and Core boundary.
- **Intent** - reject invalid input before idempotency acquisition, external calls, or publication.
- **Rule** - the controller MUST invoke the registered `IValidator<PartnerTransactionRequest>` before workflow side effects. Validation rules MUST live in Core and MUST NOT be duplicated in messaging or integration adapters.
- **Approved code shape**:

```csharp
var validationResult = await validator.ValidateAsync(request, cancellationToken);
if (!validationResult.IsValid)
{
    throw new BadRequestException(BuildValidationMessage(validationResult));
}
```

- **Avoid** - validating only after acquiring an idempotency key, duplicating field rules in controllers, or allowing invalid requests to call dependencies.
- **Implementation references** - `PartnerTransactionRequestValidator`; `PartnerTransactionsController.CreateAsync`.
- **Verification** - validator tests, controller validation tests, and bad-request API host test.

### Pattern: Middleware-owned API-key gate

- **Applies to** - partner-facing API requests; API-specific.
- **Intent** - stop unauthenticated traffic before controller and dependency execution.
- **Rule** - API-key enforcement MUST remain in middleware and MUST fail closed when enabled. The header name and key MUST come from configuration. The `/healthz` endpoint MUST remain accessible to platform probes without the partner key.
- **Approved code shape**:

```csharp
if (request.Path.StartsWithSegments("/healthz"))
{
    await next(context);
    return;
}

if (!headers.TryGetValue(options.HeaderName, out var provided)
    || !string.Equals(provided.ToString(), options.ApiKey, StringComparison.Ordinal))
{
    response.StatusCode = StatusCodes.Status401Unauthorized;
    await response.WriteAsJsonAsync(new { error = "Unauthorized" });
    return;
}

await next(context);
```

- **Avoid** - hard-coded keys, authentication logic in controllers, accepting partial values, or protecting health probes with partner credentials.
- **Implementation references** - `ApiKeyMiddleware`; `ApiKeyOptions`; shared middleware registration.
- **Verification** - `ApiKeyMiddlewareTests`, `ApiSecurityHostTests`, and the health startup test.

### Pattern: Validate correlation at the request boundary

- **Applies to** - inbound correlation context; cross-cutting middleware.
- **Intent** - preserve caller correlation when safe and provide a deterministic fallback for logs, traces, responses, and messages.
- **Rule** - correlation resolution MUST occur before API-key enforcement and controller execution. Caller values MUST be trimmed, limited to 128 characters, and rejected when they contain control characters. The resolved value MUST be added to request items, the logging scope, and the current trace.
- **Approved code shape**:

```csharp
var correlationId = ResolveCorrelationId(context);
if (correlationId is null)
{
    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    await context.Response.WriteAsJsonAsync(new { error = "Correlation-Id header is invalid." });
    return;
}

context.Items[CorrelationIdItemKey] = correlationId;
Activity.Current?.SetTag("app.correlation_id", correlationId);

using (LogContext.PushProperty("CorrelationId", correlationId))
{
    await next(context);
}
```

- **Avoid** - trusting unrestricted caller values, creating unrelated correlation IDs in each layer, or placing raw idempotency keys in the logging scope.
- **Implementation references** - `CorrelationContextMiddleware`; `PartnerTransactionsController` correlation lookup; transaction envelope metadata.
- **Verification** - `CorrelationContextMiddlewareTests` and controller correlation tests.

### Pattern: Central exception-to-HTTP translation

- **Applies to** - exceptions raised after middleware forwards a request; API-specific.
- **Intent** - produce stable status categories and avoid exposing internal exception detail for unexpected failures.
- **Rule** - known domain and upstream exceptions MUST be translated by the registered `IExceptionHandler`. Unexpected exceptions MUST return generic HTTP `500` ProblemDetails. Feature code MUST throw the established exception category instead of constructing ad hoc HTTP responses.
- **Approved code shape**:

```csharp
var problem = exception switch
{
    BadRequestException value => new ProblemDetails { Status = 400, Title = "Bad Request", Detail = value.Message, Type = "https://httpstatuses.com/400" },
    NotFoundException value => new ProblemDetails { Status = 404, Title = "Not Found", Detail = value.Message, Type = "https://httpstatuses.com/404" },
    ConflictException value => new ProblemDetails { Status = 409, Title = "Conflict", Detail = value.Message, Type = "https://httpstatuses.com/409" },
    UnauthorizedAccessException value => new ProblemDetails { Status = 401, Title = "Unauthorized", Detail = value.Message, Type = "https://httpstatuses.com/401" },
    UpstreamTimeoutException value => new ProblemDetails { Status = 408, Title = "Request Timeout", Detail = value.Message, Type = "https://httpstatuses.com/408" },
    UpstreamServiceUnavailableException value => new ProblemDetails { Status = 503, Title = "Service Unavailable", Detail = value.Message, Type = "https://httpstatuses.com/503" },
    _ => new ProblemDetails { Status = 500, Title = "Internal Server Error", Detail = "An unexpected error occurred.", Type = "https://httpstatuses.com/500" }
};

context.Response.StatusCode = problem.Status!.Value;
context.Response.ContentType = "application/problem+json";
var payload = JsonSerializer.Serialize(problem);
await context.Response.WriteAsync(payload, cancellationToken);
```

- **Avoid** - repeated try/catch-to-response mappings in controllers, leaking stack traces, or documenting middleware-owned JSON rejections as ProblemDetails.
- **Implementation references** - `ApiExceptionHandler`; Core exception types; shared exception-handler registration.
- **Verification** - `ApiExceptionHandlerTests` and `ApiExceptionMappingHostTests`.

## Configuration Contract

| Section/key | Default or rule | Purpose |
|---|---|---|
| `Security:Enabled` | `true` | Enables or bypasses API-key enforcement. |
| `Security:HeaderName` | `X-API-Key` | Selects the request header inspected by the middleware. |
| `Security:ApiKey` | Required operational value when protection is enabled | Supplies the complete expected key; production values must come from a secure configuration source. |
| `Correlation-Id` request header | Optional; maximum 128 characters and no control characters | Supplies caller-owned business correlation. |

## Source Ownership

| Project or abstraction | Responsibility |
|---|---|
| `TransactionValidation.Core` | Request model, validator, exception categories, and runtime-neutral contracts. |
| `TransactionValidation.Configuration` | API-key options, correlation middleware, API-key middleware, exception handler, and middleware ordering. |
| `TransactionValidation.Api` | Explicit validation invocation, workflow boundary, and declared response contracts. |
| `TransactionValidation.Tests` | Unit and integration-host verification of validation, middleware, and error mapping. |

## Verification Surface

| Verification | Proves |
|---|---|
| `PartnerTransactionRequestValidatorTests` | Required fields, positive amount, timestamp, and ISO currency behavior. |
| `PartnerTransactionsControllerTests` | Validation occurs before processing and correlation reaches accepted responses. |
| `ApiKeyMiddlewareTests` | Enabled, disabled, missing, wrong, valid, and health-bypass behavior. |
| `CorrelationContextMiddlewareTests` | Caller value, fallback generation, trimming, length, control-character, log-scope, and trace-tag behavior. |
| `ApiExceptionHandlerTests` | Stable exception-to-status ProblemDetails mapping. |
| API security and exception host tests | Middleware and exception behavior through the real ASP.NET Core pipeline. |

## Operational Boundaries

- API-key authentication is a shared-secret boundary; JWT, mTLS, authorization policies, rate limiting, and key rotation are not implemented by the application.
- API-key comparison is ordinal equality; the implementation does not claim hashing or constant-time comparison.
- Middleware-generated invalid-correlation and unauthorized responses are JSON but not RFC 7807 ProblemDetails.
- The default development key in local settings is not suitable for deployment; deployed secrets must come from the configured secret-delivery path.
- Correlation validates length and control characters but does not enforce a GUID format.

## Related Supporting Documents

- [Runtime and API](../runtime-and-api/README.md)
- [Architecture design overview](../../architecture/Architecture_design.md)
- [API idempotency flow and semantics](../../architecture/api_idempotency_flow_and_semantics.md)
- [Logging standards](../../operations/diagnostics/logging_standards.md)
