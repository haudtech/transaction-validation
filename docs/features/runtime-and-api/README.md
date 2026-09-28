# Runtime and API

## Purpose and Scope

This document defines the implemented runtime and HTTP-hosting model for TransactionValidation. It covers the .NET hosts, public API boundary, endpoint orchestration, API discovery, configuration precedence, runtime composition, and host-level endpoints.

It does not define validation/security rules, resilience internals, idempotency storage mechanics, broker topology, telemetry internals, or deployment procedures. Those concerns belong to their corresponding feature and supporting documents.

## Behavior Inventory

- API and Mock executable hosting
- Versioned partner transaction endpoint
- Development-only API discovery
- Shared layered configuration
- Runtime broker and idempotency-store selection
- Stable host health endpoints

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| API hosting | .NET 8 and ASP.NET Core Web API | Minimal composition root with controller endpoints | `TransactionValidation.Api` `Program` | Solution build and API startup integration tests |
| Transaction endpoint | ASP.NET Core MVC | Versioned controller route with asynchronous orchestration | `PartnerTransactionsController.CreateAsync` | Controller unit tests and API host integration tests |
| API discovery | Swashbuckle/OpenAPI | Swagger services with Development-only middleware | API `Program` | `ApiStartupHostTests.GetSwagger_WhenEnvironmentIsDevelopment_ReturnsSuccess` |
| Shared configuration | ASP.NET Core configuration and DotNetEnv | Ordered JSON, local `.env`, environment-variable, and command-line sources | `ConfigurationBuilderExtensions` | Configuration-focused build/startup behavior |
| Runtime composition | Microsoft dependency injection | Shared services plus one selected broker implementation and one selected idempotency store | API and Mock `Program`, `BrokerRegistrationExtensions`, `IdempotencyStoreRegistration` | Broker registration, idempotency registration, and startup tests |
| Host diagnostics | ASP.NET Core health checks | Unauthenticated `/healthz` endpoint on both executable hosts | API and Mock `Program` | API startup and health-check unit tests |

## Definitions and Semantics

### Executable hosts

The solution has two ASP.NET Core executable hosts:

- **API host** - accepts partner transaction requests and coordinates validation, idempotency, partner verification, publication, and the accepted response.
- **Mock host** - simulates the upstream partner-verification API and runs the primary and audit consumers selected for the active broker.

Both hosts use the shared configuration loader, Serilog host setup, OpenTelemetry registration, controllers, and a `/healthz` endpoint. Only the API exposes Swagger, and only in the Development environment.

### Public API boundary

The transaction endpoint is `POST /api/v1/partner/transactions`. The route contains the API version as part of the URL contract. A successful fresh request or replayed duplicate returns `202 Accepted`. Detailed validation, authentication, idempotency, and failure semantics belong to their owning feature documents.

### Composition root

Each executable `Program` is the composition root for its host. It selects concrete runtime services and middleware but delegates reusable behavior to shared projects. The API selects one message broker and one idempotency-store implementation at startup. The Mock selects the matching broker consumers.

### Configuration precedence

The shared configuration loader applies this effective order from lower to higher precedence:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Values loaded from a discovered local `.env` file when the process does not already define them
4. Process or container environment variables
5. Command-line arguments

The `.env` loader uses no-clobber behavior, so an existing process environment value is not replaced by the local file.

### Host success boundaries

Host startup proves that configuration and dependency registration can build the application. It does not prove that every external dependency is reachable. The API health checks add focused publisher-registration and Redis-reachability signals; detailed health semantics belong to the observability feature.

## Implementation Pattern Rules

### Pattern: Thin executable composition root

- **Applies to** - API and Mock host startup; cross-cutting.
- **Intent** - keep executable hosts focused on selecting and composing services rather than implementing domain behavior.
- **Rule** - executable `Program` files MUST register and compose owned services, middleware, and endpoints. Reusable behavior SHOULD live in the project that owns the concern. Domain contracts MUST NOT depend on executable hosts.
- **Approved code shape**:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddTransactionValidationConfiguration(builder.Environment, args);
builder.Host.UseTransactionValidationSerilog();
builder.Services.AddControllers();
builder.Services.AddTransactionValidationCommonServices(builder.Configuration);

var app = builder.Build();
app.UseTransactionValidationCommon();
app.MapControllers();
app.MapHealthChecks("/healthz");
app.Run();
```

- **Avoid** - domain validation, broker protocol operations, resilience loops, or secret values directly in `Program`.
- **Implementation references** - API `Program`; Mock `Program`; `ServiceCollectionExtensions`; `HostBuilderExtensions`.
- **Verification** - solution build and API startup integration tests.

### Pattern: Versioned controller boundary

- **Applies to** - partner-facing HTTP endpoints; API-specific.
- **Intent** - provide a stable route contract while keeping request orchestration explicit and asynchronous.
- **Rule** - partner transaction operations MUST remain behind the versioned controller route. The controller MUST coordinate through abstractions and MUST propagate cancellation to asynchronous dependencies.
- **Approved code shape**:

```csharp
[ApiController]
[Route("api/v1/partner/transactions")]
public sealed class PartnerTransactionsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] PartnerTransactionRequest request,
        CancellationToken cancellationToken)
    {
        // Coordinate validation, idempotency, verification, and publication.
    }
}
```

- **Avoid** - unversioned duplicate routes, direct broker SDK use in controllers, or ignoring the request cancellation token.
- **Implementation references** - `PartnerTransactionsController`; `IPartnerVerifier`; `IMessagePublisher`; `IIdempotencyStore`.
- **Verification** - controller unit tests and API host integration tests.

### Pattern: Development-only API discovery

- **Applies to** - Swagger/OpenAPI UI; API-specific and environment-specific.
- **Intent** - make the API discoverable during development without automatically exposing the interactive UI in other environments.
- **Rule** - Swagger services MUST be registered by the API host. Swagger middleware and UI MUST be enabled only when the host environment is Development unless a separately reviewed deployment policy changes that boundary.
- **Approved code shape**:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

- **Avoid** - unconditional Swagger UI exposure or duplicating Swagger setup in feature controllers.
- **Implementation references** - API `Program`; `TransactionValidation.Api.csproj`.
- **Verification** - `ApiStartupHostTests.GetSwagger_WhenEnvironmentIsDevelopment_ReturnsSuccess`.

### Pattern: Shared layered configuration

- **Applies to** - both executable hosts; cross-cutting.
- **Intent** - provide deterministic environment overrides while supporting local `.env` files without overwriting process values.
- **Rule** - executable hosts MUST use `AddTransactionValidationConfiguration`. Secrets MUST NOT be committed to JSON settings. Runtime environment variables and command-line arguments MUST remain higher precedence than JSON defaults.
- **Approved code shape**:

```csharp
builder.Configuration.AddTransactionValidationConfiguration(
    builder.Environment,
    args);
```

- **Avoid** - rebuilding separate configuration pipelines in each executable, hard-coded credentials, or loading `.env` values over existing process values.
- **Implementation references** - `ConfigurationBuilderExtensions.AddTransactionValidationConfiguration`.
- **Verification** - executable startup, configuration binding tests, and environment-specific host tests.

### Pattern: Single runtime implementation selection

- **Applies to** - broker and idempotency composition; API and Mock hosts.
- **Intent** - activate one concrete transport and the appropriate state implementation without changing application contracts.
- **Rule** - runtime selection MUST occur once in the composition root. Consumers and publishers MUST use the same selected broker mode. The API MUST select Redis idempotency only when its connection is configured and otherwise use the in-memory fallback.
- **Approved code shape**:

```csharp
builder.Services.AddConfiguredBroker(
    builder.Configuration,
    RegisterRabbitMq,
    RegisterAzureServiceBus);

IdempotencyStoreRegistration.Add(
    builder.Services,
    builder.Configuration);
```

- **Avoid** - selecting brokers inside controllers, registering both publishers as the active `IMessagePublisher`, or branching on infrastructure type in Core contracts.
- **Implementation references** - API `Program`; Mock `Program`; `BrokerRegistrationExtensions`; `IdempotencyStoreRegistration`.
- **Verification** - `BrokerRegistrationExtensionsTests`, `IdempotencyStoreRegistrationTests`, and Azure-broker startup integration test.

### Pattern: Stable host diagnostics endpoint

- **Applies to** - API and Mock hosts; cross-cutting.
- **Intent** - expose a stable endpoint for host and deployment health inspection.
- **Rule** - both executable hosts MUST map `/healthz`. The API-key middleware MUST allow this endpoint without partner credentials so platform probes can call it.
- **Approved code shape**:

```csharp
builder.Services.AddHealthChecks();
app.MapHealthChecks("/healthz");
```

- **Avoid** - requiring the partner API key for platform probes or treating a basic host health result as proof that every downstream workflow succeeds.
- **Implementation references** - API `Program`; Mock `Program`; `ApiKeyMiddleware`; API health-check classes.
- **Verification** - `ApiStartupHostTests.GetHealth_WhenApiKeyIsMissing_ReturnsSuccess`, `ApiKeyMiddlewareTests`, and `HealthChecksTests`.

## Configuration Contract

| Setting or source | Purpose | Boundary |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Selects the standard host environment and environment-specific JSON settings. | Process or container setting; not a secret. |
| `EVN` | Legacy environment-name override supported by the shared loader. | Takes precedence when non-empty; retain only for compatibility. |
| `.env` | Supplies local development values without committing them to application JSON. | Loaded with no-clobber behavior and must not be committed with secrets. |
| `appsettings.json` | Defines host and feature defaults. | Lowest runtime configuration layer after sources are rebuilt. |
| `appsettings.{Environment}.json` | Overrides defaults for the selected environment. | Must not contain production secrets. |
| Environment variables | Override JSON and supply container/cloud configuration. | Use hierarchical double-underscore names for nested sections. |
| Command-line arguments | Apply the highest-precedence runtime overrides. | Intended for explicit process startup overrides. |

Feature-specific sections such as `Security`, `Idempotency`, `Redis`, `PartnerVerification`, `RabbitMq`, `ServiceBusPublisher`, `OpenTelemetry`, and `ApplicationInsights` are defined by their owning feature documents.

## Source Ownership

| Project or abstraction | Runtime/API responsibility |
|---|---|
| `TransactionValidation.Api` | Public API host, controller mapping, API composition, Swagger, and API health checks. |
| `TransactionValidation.Mock` | Mock dependency host, consumer composition, observation endpoints, and basic health endpoint. |
| `TransactionValidation.Configuration` | Shared configuration loading, middleware/service registration, Serilog, telemetry, and broker selection helper. |
| `TransactionValidation.Core` | Runtime-neutral request, response, envelope, exception, validation, and interface contracts. |
| `TransactionValidation.Integration` | External partner-verification adapter used by API orchestration. |
| `TransactionValidation.Messaging` | Concrete broker publishers and supporting broker clients. |

## Verification Surface

| Verification | Proves |
|---|---|
| Solution build | Both web hosts and their project dependency graph compile. |
| `ApiStartupHostTests` | API startup, Development Swagger, health endpoint access, and Azure broker selection. |
| `PartnerTransactionsControllerTests` | Controller orchestration and accepted-response behavior. |
| `BrokerRegistrationExtensionsTests` | Exactly one supported broker registration path is selected. |
| `IdempotencyStoreRegistrationTests` | The configured state implementation is selected at composition time. |
| `HealthChecksTests` | API publisher-registration and Redis health outcomes. |

## Operational Boundaries

- Swagger UI is exposed only in Development.
- The route prefix supplies API versioning; no separate API-versioning library is configured.
- The Mock host is a validation dependency and consumer host, not a production partner system.
- A successful host start does not prove external broker, partner API, or Azure telemetry ingestion unless a focused health or runtime test exercises it.
- The `EVN` setting is a legacy compatibility override; `ASPNETCORE_ENVIRONMENT` is the standard host setting.

## Related Supporting Documents

- [Architecture design overview](../architecture_design/Architecture_design.md)
- [API idempotency flow and semantics](../architecture_design/api_idempotency_flow_and_semantics.md)
- [Documentation index](../README.md)
