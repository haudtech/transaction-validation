# Observability

## Purpose and Scope

This document defines the observability behavior implemented by TransactionValidation. It covers Serilog host integration, source-generated structured events, request and message correlation, OpenTelemetry traces and metrics, operation timing, health signals, local console export, and conditional Azure Monitor export.

It does not claim application dashboards, alerting rules, sampling controls, ingestion caps, service-level objectives, broker-consumer spans, or verified Azure telemetry ingestion.

## Behavior Inventory

- Shared Serilog host configuration
- Source-generated logging catalogs and stable event IDs
- Structured operational fields and sensitive-data exclusions
- API request correlation and trace enrichment
- Message and correlation identity propagation
- ASP.NET Core and outbound HTTP tracing
- ASP.NET Core and outbound HTTP metrics
- Optional EF Core instrumentation registration
- Local console telemetry export
- Conditional Azure Monitor export
- API dependency and host health endpoints
- Azure Log Analytics and Application Insights resource wiring

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Application logging | Serilog | Shared host extension reads per-host configuration and enriches from log context | `HostBuilderExtensions`, app settings | Observability registration test and solution build |
| Event definitions | `LoggerMessage` source generation | Static partial catalogs with stable event IDs and typed parameters | Core logging catalogs | Logger contract test and analyzer-enforced build |
| Logging enforcement | .NET analyzers | `CA1848` and `CA2254` are build errors; no direct logger extension calls in production source | `.editorconfig` | Format/build validation and source scan |
| Request correlation | ASP.NET Core middleware, `Activity`, Serilog `LogContext` | Validate/generate business correlation and attach it to request scope and trace | `CorrelationContextMiddleware` | Correlation middleware tests |
| Message correlation | Shared envelope and broker metadata | Carry message and correlation IDs beyond the HTTP request | Controller and messaging publishers | Controller, publisher, and E2E tests |
| Distributed tracing | OpenTelemetry | ASP.NET Core instrumentation when enabled and HTTP client instrumentation for both hosts | `ServiceCollectionExtensions` | Observability registration tests |
| Metrics | OpenTelemetry | ASP.NET Core and HTTP client meter instrumentation for both hosts | `ServiceCollectionExtensions` | Meter-provider registration test |
| Local telemetry | OpenTelemetry console exporter | Export traces and metrics when exporter is `Console` | Shared observability registration | Registration test and local configuration |
| Azure telemetry | Azure Monitor OpenTelemetry distribution | Enable export when direct or Application Insights connection string is present | Shared registration and Container Apps configuration | Registration tests and Bicep build |
| Platform logs | Azure Container Apps and Log Analytics | Collect container stdout/stderr through the managed environment | Container Apps Bicep module | Bicep build |
| Azure resources | Application Insights and Log Analytics | Workspace-based Application Insights with 30-day retention | Observability Bicep module | Bicep build |
| Health reporting | ASP.NET Core health checks | API publisher registration and conditional Redis ping; basic Mock host health | API/Mock hosts and health checks | Health-check unit and host tests |

## Definitions and Semantics

### Logs, traces, metrics, and health

| Signal | Implemented purpose |
|---|---|
| Logs | Record discrete transaction, dependency, idempotency, publishing, topology, and consumer outcomes. |
| Traces | Record incoming ASP.NET Core requests and outbound `HttpClient` dependencies. |
| Metrics | Record ASP.NET Core and outbound HTTP measurements. |
| Health | Expose current host/dependency checks through `/healthz`. |

These signals are complementary. A structured log event is not a trace span, and a healthy endpoint does not prove that a complete transaction or telemetry-export path succeeds.

### Logging catalogs

| Catalog | Event IDs | Implemented responsibility |
|---|---:|---|
| `TransactionValidationLogging` | `1000-1006` | Request, validation, duplicate replay, idempotency conflict, verification timing, publication timing, and processing failure. |
| `TransactionValidationLogging` | `1100-1101` | Partner API completion and failure. |
| `TransactionValidationLogging` | `1200-1203` | Idempotency acquisition, lookup, storage, and release outcomes. |
| `MessagingLogging` | `1300-1304` | Publish completion/failure and consumer observation/retry/failure. |
| `RabbitMqLogging` | `1400-1404` | RabbitMQ topology initialization and consumer startup. |
| `ServiceBusLogging` | `1405-1406` | Service Bus consumer startup. |

Each caller injects its own `ILogger<T>` and passes it to a catalog method. The typed caller remains the logging category while event IDs, levels, templates, and property names stay centralized.

### Structured fields

Implemented events use typed fields such as:

- `CorrelationId`
- `MessageId`
- `PartnerId`
- `TransactionReference`
- `DurationMs`
- `Dependency`
- `Outcome`
- `StatusCode`
- `ErrorType`
- consumer and destination metadata

The transaction logger contract test explicitly excludes `IdempotencyKey` from the approved transaction event fields. Production source contains no direct `LogInformation`, `LogWarning`, or `LogError` extension calls.

### Correlation identities

The API request pipeline keeps two identities separate:

- **Trace ID** - technical identifier from `Activity.Current` used by OpenTelemetry.
- **Correlation ID** - application/message identifier supplied through a valid `Correlation-Id` header or generated from request context.

The API correlation middleware:

1. validates or generates the correlation value
2. stores it in `HttpContext.Items`
3. adds `app.correlation_id` to the current activity
4. pushes `CorrelationId` and `TraceId` into the Serilog request scope
5. removes scoped log properties when the request completes

The controller returns the correlation ID in accepted responses and copies it to `TransactionEnvelope`. Publishers propagate it in broker metadata. The Mock host does not register the API correlation middleware, and consumers do not create linked OpenTelemetry activities.

### Timing measurements

Application code records elapsed milliseconds in generated log events for:

- controller-level partner verification
- complete transaction processing failures
- integration-client verification outcomes
- RabbitMQ and Service Bus publication outcomes

These are event-level durations. They are not histograms, service-level indicators, or alert thresholds.

### OpenTelemetry registration

Both API and Mock hosts call `AddTransactionValidationObservability` with distinct service names. Registration provides:

- ASP.NET Core trace instrumentation when enabled
- HTTP client trace instrumentation
- ASP.NET Core metrics instrumentation
- HTTP client metrics instrumentation
- optional EF Core trace instrumentation when enabled
- console trace and metric exporters when `Exporter` equals `Console`
- Azure Monitor registration when a supported connection string is non-empty

The `UseAspNetCoreInstrumentation` option controls trace instrumentation only. HTTP client tracing is always registered. ASP.NET Core and HTTP client metrics are always registered by the shared method.

EF Core instrumentation is enabled in current API and Mock settings, but no `DbContext` or EF Core data-access workflow exists in the solution. Registration therefore does not imply emitted database spans.

### Export paths

#### Local path

When `OpenTelemetry:Tracing:Exporter` is `Console`, both traces and metrics use console exporters. API Development Serilog settings request console and rolling-file sinks. Mock settings request the compact JSON console formatter.

#### Azure path

When either `OpenTelemetry:Tracing:AzureMonitor:ConnectionString` or the fallback `ApplicationInsights:ConnectionString` is supplied, the shared registration enables Azure Monitor export.

Bicep provisions:

- a Log Analytics workspace with 30-day retention
- a workspace-based Application Insights component with 30-day retention
- a Key Vault secret containing the Application Insights connection string
- secret-backed `APPLICATIONINSIGHTS__CONNECTIONSTRING` variables for both Container Apps
- Container Apps environment log collection into Log Analytics

API production Serilog configuration writes to the console but does not configure the compact JSON formatter. Mock production settings request the compact JSON formatter. No direct Serilog-to-Application-Insights sink is registered.

### Health signals

The API `/healthz` endpoint includes:

- publisher registration resolution
- Redis ping when distributed idempotency is configured
- a healthy in-memory-mode result when Redis is absent

The Mock `/healthz` endpoint exposes the default host health registration without custom broker checks.

## Implementation Pattern Rules

### Pattern: Define production events with source generation

- **Applies to** - application logging in `src`; cross-cutting.
- **Intent** - keep event IDs, levels, templates, and structured fields stable and compile-time validated.
- **Rule** - production events MUST use a static partial logging catalog and `[LoggerMessage]`. Direct `ILogger` extension calls MUST NOT be added because `CA1848` and `CA2254` are build errors.
- **Approved code shape**:

```csharp
[LoggerMessage(
    EventId = 1300,
    Level = LogLevel.Information,
    Message = "Message publish completed. Dependency={Dependency} CorrelationId={CorrelationId} MessageId={MessageId} DurationMs={DurationMs}")]
public static partial void MessagePublishCompleted(
    ILogger logger,
    string dependency,
    string correlationId,
    string messageId,
    double durationMs);
```

- **Avoid** - direct `logger.LogInformation(...)`, runtime-built templates, reused event IDs, or payload/credential fields.
- **Implementation references** - all four Core logging catalogs; `.editorconfig`.
- **Verification** - solution build, source scan, and logging contract tests.

### Pattern: Preserve the emitting class as logger category

- **Applies to** - generated logging call sites; cross-cutting.
- **Intent** - centralize event definitions without losing the class-specific logging category.
- **Rule** - owning classes MUST inject `ILogger<TClass>` and pass it to static catalog methods. Logging catalogs MUST NOT become injected wrapper services or shared logger instances.
- **Approved code shape**:

```csharp
public sealed class Publisher
{
    private readonly ILogger<Publisher> _logger;

    public void Record(string correlationId, string messageId, double durationMs)
    {
        MessagingLogging.MessagePublishCompleted(
            _logger,
            "Broker",
            correlationId,
            messageId,
            durationMs);
    }
}
```

- **Avoid** - a global logger category, one-line wrapper methods around every event, or an `ILogger`-derived domain interface.
- **Implementation references** - controller, integration client, stores, publishers, topology initializer, and consumers.
- **Verification** - source review and host build.

### Pattern: Keep structured telemetry free of sensitive values

- **Applies to** - logging and custom telemetry fields; cross-cutting.
- **Intent** - preserve useful operational context without exposing credentials or financial payloads.
- **Rule** - events MUST NOT include API keys, passwords, connection strings, tokens, raw request bodies, transaction amounts, or raw idempotency keys. Event methods SHOULD accept only the fields needed by that event.
- **Approved code shape**:

```csharp
TransactionValidationLogging.TransactionPublished(
    logger,
    correlationId,
    partnerId,
    transactionReference,
    messageId,
    durationMs);
```

- **Avoid** - logging complete DTOs, request headers, broker credentials, or arbitrary caller-supplied idempotency values.
- **Implementation references** - logging catalogs and transaction logger property-allowlist test.
- **Verification** - `TransactionValidationLoggerTests` and source review.

### Pattern: Establish correlation once at the API boundary

- **Applies to** - API HTTP requests; API-specific.
- **Intent** - use one application correlation value across logs, traces, responses, envelopes, and broker metadata.
- **Rule** - the API MUST resolve correlation in middleware before authentication and controller execution. The resolved value MUST enter the request log scope and current activity, and downstream code MUST reuse it rather than generate unrelated values.
- **Approved code shape**:

```csharp
context.Items[CorrelationIdItemKey] = correlationId;
Activity.Current?.SetTag("app.correlation_id", correlationId);

using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
{
    await next(context);
}
```

- **Avoid** - separate correlation IDs per layer, replacing the technical trace ID with the application correlation ID, or assuming Mock requests receive this middleware.
- **Implementation references** - `CorrelationContextMiddleware`; controller; envelope and publisher metadata.
- **Verification** - correlation middleware, controller correlation, publisher, and E2E tests.

### Pattern: Measure owned operation boundaries

- **Applies to** - transaction, external dependency, and message publication operations; cross-cutting.
- **Intent** - expose elapsed time at meaningful ownership boundaries for troubleshooting.
- **Rule** - components SHOULD measure and emit duration for operations they own. A success SHOULD NOT be logged redundantly by every layer. Failures SHOULD include the exception at the layer that owns the failed operation.
- **Approved code shape**:

```csharp
var stopwatch = Stopwatch.StartNew();
try
{
    await dependency.ExecuteAsync(cancellationToken);
    Logging.OperationCompleted(logger, stopwatch.Elapsed.TotalMilliseconds);
}
catch (Exception exception)
{
    Logging.OperationFailed(logger, exception, stopwatch.Elapsed.TotalMilliseconds);
    throw;
}
```

- **Avoid** - timing every helper method, logging the same success at multiple layers, or swallowing an exception after logging it.
- **Implementation references** - controller, `PartnerVerifierClient`, RabbitMQ publisher, and Service Bus publisher.
- **Verification** - logger contract tests and focused operation tests.

### Pattern: Register one shared telemetry pipeline per host

- **Applies to** - API and Mock composition roots; cross-cutting.
- **Intent** - keep instrumentation and export behavior consistent while preserving distinct service names.
- **Rule** - both executable hosts MUST call `AddTransactionValidationObservability` with their own service name. Instrumentation MUST be configured in the shared extension rather than duplicated in `Program`.
- **Approved code shape**:

```csharp
builder.Services.AddTransactionValidationObservability(
    builder.Configuration,
    "TransactionValidation.Api");
```

- **Avoid** - separate telemetry pipelines with divergent instrumentation, shared service names for both hosts, or exporter creation in controllers.
- **Implementation references** - API and Mock `Program`; `ServiceCollectionExtensions`.
- **Verification** - observability registration tests and solution build.

### Pattern: Enable exporters from explicit configuration

- **Applies to** - console and Azure telemetry export; environment-specific.
- **Intent** - keep local diagnostics available and cloud export conditional on supplied configuration.
- **Rule** - console trace and metric exporters MUST be added only when exporter mode is `Console`. Azure Monitor MUST be added only when the direct or fallback connection string is non-empty. Connection strings MUST NOT be committed to application settings.
- **Approved code shape**:

```csharp
if (string.Equals(exporterName, "Console", StringComparison.OrdinalIgnoreCase))
{
    tracing.AddConsoleExporter();
    metrics.AddConsoleExporter();
}

if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
{
    services.AddOpenTelemetry().UseAzureMonitor(options =>
        options.ConnectionString = azureMonitorConnectionString);
}
```

- **Avoid** - unconditional cloud export, hard-coded connection strings, or claiming export succeeded because registration completed.
- **Implementation references** - shared observability registration; OpenTelemetry settings; Container Apps/Key Vault Bicep.
- **Verification** - observability registration tests and Bicep build.

### Pattern: Keep logs and telemetry export paths distinct

- **Applies to** - Azure-hosted logging and OpenTelemetry; environment-specific.
- **Intent** - avoid duplicate application-log ingestion while preserving platform logs and application telemetry.
- **Rule** - Serilog MUST write through configured sinks, with production container logs written to console. Container Apps log collection and OpenTelemetry Azure Monitor export MUST remain separate paths. A direct Serilog Application Insights sink MUST NOT be assumed.
- **Approved code shape**:

```text
Serilog event -> console -> Container Apps log collection -> Log Analytics
OpenTelemetry trace/metric -> Azure Monitor exporter -> Application Insights
```

- **Avoid** - documenting Serilog events as Application Insights spans, adding duplicate sinks without cost/duplication review, or claiming API console output is compact JSON when it is not configured that way.
- **Implementation references** - Serilog app settings, `HostBuilderExtensions`, Container Apps environment, and observability Bicep.
- **Verification** - configuration review, host build, and Bicep build.

### Pattern: Report only implemented health dependencies

- **Applies to** - `/healthz`; host-specific.
- **Intent** - provide truthful health signals without treating service registration as full dependency connectivity.
- **Rule** - the API health endpoint MUST report publisher resolution and conditional Redis reachability. The Mock health endpoint MUST be described as basic host health until custom checks are registered.
- **Approved code shape**:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MessagingHealthCheck>("messaging")
    .AddCheck<RedisHealthCheck>("redis");

app.MapHealthChecks("/healthz");
```

- **Avoid** - claiming publisher resolution proves broker connectivity or claiming the Mock endpoint checks consumer/broker health.
- **Implementation references** - API and Mock `Program`; `MessagingHealthCheck`; `RedisHealthCheck`.
- **Verification** - health-check unit tests and API host health test.

## Configuration Contract

| Section/key | Default or behavior | Purpose |
|---|---|---|
| `Serilog:MinimumLevel` | Host settings define defaults and namespace overrides | Controls event filtering. |
| `Serilog:WriteTo` | API base/Production console; API Development requests console and rolling file; Mock requests compact JSON console | Selects configured log destinations and formatting. |
| `Serilog:Enrich` | `FromLogContext` | Includes request-scoped correlation fields. |
| `Serilog:Properties:Application` | Host-specific | Identifies API or Mock log source. |
| `OpenTelemetry:Tracing:UseAspNetCoreInstrumentation` | `true` in current host settings | Enables incoming request trace instrumentation. |
| `OpenTelemetry:Tracing:UseEntityFrameworkCoreInstrumentation` | `true` in current host settings | Registers EF instrumentation; no current `DbContext` emits spans. |
| `OpenTelemetry:Tracing:Exporter` | `Console` locally; `AzureMonitor` in Production settings | Controls console exporter registration; Azure export still requires a connection string. |
| `OpenTelemetry:Tracing:AzureMonitor:ConnectionString` | Empty in committed settings | Direct Azure Monitor connection-string source. |
| `ApplicationInsights:ConnectionString` | Empty in committed settings | Fallback Azure Monitor connection-string source. |
| `APPLICATIONINSIGHTS__CONNECTIONSTRING` | Secret-backed in Azure Container Apps | Supplies the deployed fallback connection string. |

## Source Ownership

| Project or surface | Responsibility |
|---|---|
| `TransactionValidation.Core` | Generated logging catalogs and transaction log context. |
| `TransactionValidation.Configuration` | Serilog host setup, correlation middleware, OpenTelemetry instrumentation/export registration, and options. |
| `TransactionValidation.Api` | Request/workflow timing, API health checks, and API host registration. |
| `TransactionValidation.Integration` | Partner dependency timing and outcome events. |
| `TransactionValidation.Messaging` | Publish timing and RabbitMQ topology events. |
| `TransactionValidation.Mock` | Consumer lifecycle/outcome events and Mock host telemetry registration. |
| `infra/bicep` | Log Analytics, Application Insights, Key Vault secret, Container Apps log collection, and environment injection. |
| `TransactionValidation.Tests` | Registration, correlation, event-property, health, and behavior verification. |

## Verification Surface

| Verification | Proves |
|---|---|
| `ObservabilityRegistrationTests` | Trace/metric providers, console mode, Azure connection-string fallbacks, and shared Serilog host setup register successfully. |
| `CorrelationContextMiddlewareTests` | Correlation resolution, validation, activity tag, request context, and middleware flow. |
| `TransactionValidationLoggerTests` | Transaction event IDs/properties and raw idempotency-key exclusion. |
| `HealthChecksTests` | Publisher resolution and conditional Redis health results. |
| Publisher, verifier, store, and consumer tests | Operations invoke their generated logging paths without changing public behavior. |
| `.editorconfig` plus solution build | Direct logger extension calls and dynamic templates are rejected in production source. |
| Bicep build | Observability resources, secret propagation, and Container Apps log configuration compile. |

## Operational Boundaries

- No application dashboards, KQL workbooks, alert rules, daily ingestion cap, or explicit telemetry sampling configuration are provisioned.
- Azure telemetry registration tests prove service registration, not successful ingestion into a deployed Application Insights resource.
- Log Analytics and Application Insights retention are configured to 30 days; no 5-day policy exists in current Bicep.
- API console logging uses the configured console sink without compact JSON formatting. Mock settings name `CompactJsonFormatter`.
- The repository does not explicitly reference `Serilog.Sinks.File` or `Serilog.Formatting.Compact`; current tests do not prove those named sink/formatter components are activated at runtime.
- EF Core instrumentation is registered because current settings enable it, but no `DbContext` or database workflow exists.
- Consumers do not create broker producer/consumer `Activity` spans or links.
- The Mock host does not apply the API correlation middleware.
- Messaging health checks resolve the active publisher but do not perform a live broker operation.
- Structured-property allowlist testing currently covers `TransactionValidationLogging`; it does not exhaustively validate every messaging catalog event.
- No direct Serilog-to-Application-Insights sink is configured.

## Related Supporting Documents

- [Logging standards](../../operations/diagnostics/logging_standards.md)
- [Runtime and API](../runtime-and-api/README.md)
- [Validation and security](../validation-and-security/README.md)
- [Messaging](../messaging/README.md)
- [Architecture design overview](../architecture_design/Architecture_design.md)
