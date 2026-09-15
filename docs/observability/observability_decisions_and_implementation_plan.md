# Observability Decisions and Implementation Plan

Status: Decision record and implementation plan

This document records the agreed observability model for TransactionValidation. It captures the distinction between Serilog logs, Azure Log Analytics, OpenTelemetry, and Application Insights, together with the correlation strategy and the implementation work required for the next observability pass.

## 1. Final Decisions

| Area | Decision |
|---|---|
| Application logging | Use Serilog for structured application logs. |
| Production log sink | Write Serilog logs to stdout/stderr through the console sink. Do not use a production file sink in Container Apps. |
| Log analysis | Let Azure Container Apps collect console output into the Log Analytics workspace. |
| Distributed tracing | Use OpenTelemetry for incoming ASP.NET Core requests and outbound `HttpClient` calls. |
| Database tracing | Support EF Core instrumentation now so the future `DbContext` is covered automatically. |
| Azure application telemetry | Export OpenTelemetry traces and metrics to Application Insights through Azure Monitor when a connection string is configured. |
| Local telemetry | Keep the Console exporter available for local development and diagnostics. |
| Structured logging contract | Standardize application logs through a shared source-generated `TransactionValidationLogger` event catalog with common fields and layer-owned actions. |
| Logger category | Inject `ILogger<TClass>` into each controller, service, repository, and infrastructure class so the emitting class remains the logging category. |
| Logger implementation | Use `[LoggerMessage]` source-generated methods with stable `EventId` values and typed parameters. |
| Azure telemetry scope | Export OpenTelemetry telemetry from both the API and Mock Container Apps. |
| Production log format | Use structured JSON Serilog output in all environments. |
| Correlation identifiers | Keep the technical OpenTelemetry `TraceId` separate from the application/message `CorrelationId`. |
| Application `CorrelationId` | Continue propagating it in the API response and broker message metadata, and add it to OpenTelemetry telemetry as a custom property. |
| Correlation input | Accept and validate a client-provided `Correlation-Id` header; generate one when absent. |
| Broker tracing | Create linked OpenTelemetry activities for broker consumers in this implementation. |
| Azure retention | Use 5-day retention, 10% telemetry sampling, and a 100 MB/day ingestion cap with an alert at 80%. |
| Azure secret delivery | Store the Application Insights connection string in Key Vault and expose it through the existing Container Apps secret pattern. |
| Direct Serilog-to-Application Insights sink | Do not add one by default. It would duplicate Container Apps log collection and increase ingestion complexity and cost. |

## 2. Why Logs and Application Insights Are Separate

Log Analytics and Application Insights are both Azure Monitor experiences, and Application Insights can use a Log Analytics workspace as its storage/query backend. They are still different observability paths with different telemetry models.

### Log Analytics

Log Analytics is the workspace-level destination for operational logs, including:

- Serilog console events collected from Container Apps.
- Container stdout/stderr and startup or shutdown messages.
- Container Apps revision, replica, ingress, and platform diagnostics when enabled.
- Infrastructure and service logs from other Azure resources.

Its primary question is:

> What happened across the application and platform environment?

### Application Insights

Application Insights is the application performance monitoring experience for structured telemetry, including:

- Incoming HTTP request spans.
- Outbound `HttpClient` dependency spans.
- Future EF Core database dependency spans.
- Distributed trace relationships and timing.
- Application exceptions and telemetry metrics.

Its primary question is:

> Why was this request slow, failed, or dependent on an unhealthy downstream service?

The systems are not separate because Azure requires unrelated stores. They are separate because an operational log event and a distributed trace span answer different questions.

## 3. Agreed Runtime Flow

```text
Serilog structured event
    -> console sink
    -> Azure Container Apps stdout/stderr collection
    -> Log Analytics workspace

OpenTelemetry request/dependency telemetry
    -> Azure Monitor OpenTelemetry exporter
    -> Application Insights
    -> usually backed by or queryable through a Log Analytics workspace
```

For a single client request, both paths may contain data:

```text
POST /api/v1/partner/transactions
    |
    +-- Serilog: "Transaction published"
    |       -> Log Analytics
    |
    +-- ASP.NET Core request span
    +-- partner verification HttpClient span
    +-- future EF Core database span
            -> Application Insights
```

The Serilog event is not automatically converted into an Application Insights request or trace record. Conversely, an Application Insights span is not a replacement for a detailed application log message.

## 4. Structured Logging Contract

The application will use a shared structured logging contract as the codebase grows beyond controllers. The contract should standardize field names and event actions while allowing each layer to log only the decisions and dependency outcomes it owns.

### Layer responsibilities

| Layer | Logging responsibility |
|---|---|
| Controller | Request boundary, validation result, idempotency result, and response outcome. |
| Service | Business decisions and workflow outcomes. |
| Repository or data access | Database or persistence operation, duration, retry, and failure. |
| Messaging/infrastructure | Broker connection, publish/consume operation, confirmation, retry, and dependency failure. |
| Middleware | Request-wide correlation scope and unhandled exception outcome. |

Successful operations should not be logged redundantly at every layer. For example, the service owns the business event that a transaction was accepted, while the messaging component owns the technical event that a broker publish completed.

### Common structured fields

Every event should include the fields that are available and relevant to its scope:

| Field | Meaning |
|---|---|
| `EventName` | Stable action name used for Log Analytics queries. |
| `EventId` | Stable numeric or symbolic identifier for the event definition. |
| `Layer` | Owning layer, such as `Controller`, `Service`, `Repository`, or `Infrastructure`. |
| `Operation` | Business or technical operation being performed. |
| `Outcome` | Normalized result, such as `Started`, `Succeeded`, `Rejected`, `Duplicate`, or `Failed`. |
| `TraceId` | OpenTelemetry technical trace identifier. |
| `CorrelationId` | Business/message correlation identifier. |
| `MessageId` | Broker message identifier when one exists. |
| `PartnerId` | Partner identifier when approved as non-sensitive. |
| `TransactionReference` | Transaction reference when approved as non-sensitive. |
| `DurationMs` | Elapsed duration for completed operations. |
| `ErrorType` | Exception type or stable error category for failures. |
| `Dependency` | Technical dependency, such as `PartnerApi`, `RabbitMQ`, `ServiceBus`, `Redis`, or `Database`. |
| `RetryCount` | Number of retries when a retry policy applies. |

Request-wide fields such as `TraceId`, `CorrelationId`, `PartnerId`, and `TransactionReference` should be added through a scoped logging context. Event-specific fields such as `Outcome`, `DurationMs`, `StatusCode`, `Dependency`, and `RetryCount` should be added by the event method.

### Standard event actions

The shared logging abstraction should expose named actions rather than allowing ad hoc message templates in each layer.

Controller actions:

- `TransactionRequestReceived`
- `TransactionRequestRejected`
- `TransactionResponseCompleted`

Service actions:

- `TransactionValidationCompleted`
- `TransactionDuplicateDetected`
- `PartnerVerificationCompleted`
- `TransactionProcessingCompleted`
- `TransactionProcessingFailed`

Repository and infrastructure actions:

- `RepositoryOperationCompleted`
- `RepositoryOperationFailed`
- `MessagePublishCompleted`
- `MessagePublishFailed`
- `DatabaseQueryCompleted`
- `DatabaseQueryFailed`

The standard implementation should be a `public static partial class TransactionValidationLogger` using `[LoggerMessage]` source-generated methods. Each owning class injects its own `ILogger<TClass>` and passes it directly to the common method. This preserves the class-specific logging category while centralizing event IDs, levels, templates, and structured property names. Do not create `ITransactionValidationLogger : ILogger`, a shared logger instance, or private one-line wrapper methods in each class.

Each event must have a stable `EventId` and a stable action/property vocabulary. Event IDs are operational contract values: they must not be reused for a different meaning after deployment.

| EventId range | Owner | Examples |
|---|---|---|
| `1000-1099` | Controller/API boundary | `TransactionRequestReceived`, `TransactionRequestRejected`, `TransactionResponseCompleted` |
| `1100-1199` | Service/business workflow | `TransactionValidationCompleted`, `TransactionProcessingCompleted`, `TransactionProcessingFailed` |
| `1200-1299` | Repository/data access | `RepositoryOperationCompleted`, `RepositoryOperationFailed`, `DatabaseQueryCompleted`, `DatabaseQueryFailed` |
| `1300-1399` | Messaging/infrastructure | `MessagePublishCompleted`, `MessagePublishFailed`, consumer and broker lifecycle events |
| `1400-1499` | Middleware/platform | correlation, exception, startup, and health-check events |

Prefer individual typed parameters in generated methods instead of passing the entire `TransactionLogContext`; each event should expose only the fields it needs. Keep `TransactionLogContext` for workflow composition where it remains useful.

### Log level policy

| Level | Use |
|---|---|
| `Information` | Normal request, business, and dependency outcomes that are useful for operations. |
| `Warning` | Expected rejection, duplicate, retry exhaustion, or degraded dependency behavior. |
| `Error` | Unexpected failure or operation failure requiring investigation. Include the exception through the logging API. |
| `Debug` | Local diagnostic detail that is too noisy for normal production operation. |

Do not log request bodies, API keys, credentials, connection strings, raw SQL parameters, or sensitive financial data. The shared abstraction should make the safe fields easy to use and the unsafe data difficult to add accidentally.

## 5. Correlation Strategy

The application has two valid identifiers with different ownership:

### OpenTelemetry `TraceId`

`TraceId` is the technical distributed-tracing identifier created by the current `Activity` and used by OpenTelemetry. Application Insights exposes it as the operation-level correlation identifier, commonly represented by `operation_Id`.

It correlates the technical execution path:

```text
API request
    -> partner verification HTTP request
    -> future database query
```

### Application `CorrelationId`

`CorrelationId` is the business and messaging identifier. The API currently derives it from `HttpContext.TraceIdentifier` in [PartnerTransactionsController.cs](../../src/TransactionValidation.Api/Controllers/PartnerTransactionsController.cs), returns it to the client, and propagates it through the transaction envelope and broker metadata.

It must remain available after the original HTTP request has finished, because the transaction continues asynchronously through RabbitMQ or Azure Service Bus.

### Final correlation decision

Do not replace the application `CorrelationId` with `TraceId`. Preserve both:

```text
TraceId
    Technical request/dependency correlation in Application Insights

CorrelationId
    Business transaction and message correlation across API and brokers
```

The next implementation pass will attach the application `CorrelationId` to the current OpenTelemetry activity as a custom property, for example `app.correlation_id`. This makes it searchable in Application Insights without changing the API or messaging contract.

The current implementation does not yet provide this complete link. `TraceId` is available to OpenTelemetry, and `CorrelationId` is propagated in the API response and broker message metadata, but the application `CorrelationId` is not yet consistently added to the `Activity` or Serilog logging scope.

### Cross-system correlation flow

The intended flow is:

```text
Client response: correlationId
    |
    +--> Serilog LogContext: CorrelationId
    |       -> Container Apps console logs
    |       -> Log Analytics
    |
    +--> Activity.Current tag: app.correlation_id
            -> Application Insights custom dimensions
            -> operation_Id / TraceId for technical child spans
```

The business correlation value and technical trace value must remain separate:

```text
CorrelationId
    Finds the business transaction across API logs and broker messages.

operation_Id / TraceId
    Finds the technical request, HTTP dependencies, and future database spans.
```

## 6. Current Configuration and Registration

### Serilog

Serilog is configured from the `Serilog` section through `ReadFrom.Configuration(...)` in [Program.cs](../../src/TransactionValidation.Api/Program.cs) and [Mock Program.cs](../../src/TransactionValidation.Mock/Program.cs). The current profiles are:

- API base settings: console sink.
- API Development settings: console sink plus rolling file sink for local diagnostics.
- API Production settings: explicit JSON console-only profile.
- Mock base, Development, and Production settings: JSON console-only profile.

The production console-only choice is intentional because Container App filesystems are ephemeral and Azure already collects container output. JSON output is used so Log Analytics can query structured properties consistently for both services.

### OpenTelemetry

The common registration is in [ServiceCollectionExtensions.cs](../../src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs):

Both API and Mock call the shared `AddTransactionValidationObservability(configuration, serviceName)` method and the shared `UseTransactionValidationSerilog()` host extension. The service name remains app-specific (`TransactionValidation.Api` or `TransactionValidation.Mock`), while instrumentation, exporter selection, connection-string fallback, and Serilog startup behavior are centralized.

- `AddAspNetCoreInstrumentation()` creates server spans for incoming API requests.
- `AddHttpClientInstrumentation()` creates dependency spans for outbound HTTP calls.
- `AddEntityFrameworkCoreInstrumentation()` is enabled by configuration for the upcoming `DbContext`.
- `AddConsoleExporter()` is enabled when `OpenTelemetry:Tracing:Exporter` is `Console`.
- `UseAzureMonitor(...)` is enabled when either `OpenTelemetry:Tracing:AzureMonitor:ConnectionString` or `ApplicationInsights:ConnectionString` is supplied.

The EF Core package is currently pinned to `1.13.0-beta.1` because no stable `1.12.0` package is available from NuGet. This pin must be revisited when a stable compatible version is released.

## 7. Pre-Implementation Clarification Gate

The following points must be confirmed before implementing the full observability change. They affect code contracts, Azure resources, secret handling, or production queryability.

### Required decisions

| Topic | Current gap | Recommended decision |
|---|---|---|
| Application Insights resource | Bicep currently provisions Log Analytics but no Application Insights component or connection-string output. | **Decided:** provision one workspace-based Application Insights component per environment, linked to that environment's Log Analytics workspace. |
| Connection-string ownership | The application supports `APPLICATIONINSIGHTS__CONNECTIONSTRING`, but Bicep and the deployment workflow do not provide it. | **Decided:** store the connection string in Key Vault and expose it through the existing Container Apps secret pattern. |
| Telemetry scope | Both deployed services need a defined telemetry policy. | **Decided:** both API and Mock export Azure Monitor telemetry. |
| Console log format | The current Serilog console sink is configured, but its output format is not explicitly JSON. | **Decided:** use structured JSON Serilog output in all environments. |
| Correlation input | The current application value is derived from `HttpContext.TraceIdentifier`; no caller-supplied correlation header is defined. | **Decided:** accept and validate an inbound `Correlation-Id` header, generating one when absent. |
| Correlation propagation | Adding `app.correlation_id` to the request `Activity` does not by itself define propagation to asynchronous consumer activities. | **Decided:** create linked broker-consumer activities in this implementation. |
| Log and trace duplication | The design intentionally avoids a Serilog Application Insights sink. | Confirm that Application Insights traces/metrics and Log Analytics console logs are sufficient, with no direct Serilog-to-Application Insights export. |
| Data classification | Partner and transaction identifiers are proposed as searchable fields. | Confirm that `PartnerId`, `TransactionReference`, `CorrelationId`, and `MessageId` are non-sensitive operational identifiers and approved for Azure retention. |
| Retention and cost | The Log Analytics workspace has a retention setting, but Application Insights retention, sampling, and ingestion budget are not specified. | **Decided:** use 5-day retention, 10% sampling, a 100 MB/day ingestion cap, and an alert at 80%. |
| Azure permissions and networking | The deployment uses managed identities and private endpoints for core dependencies, but observability permissions are not documented. | **Decided:** reuse the existing user-assigned identities and outbound network path; validate Azure Monitor connectivity. |
| Validation access | The document has KQL examples but no agreed resource/table names or Azure validation identity. | **Decided:** perform final KQL validation manually in the Azure Portal or Log Analytics workspace. |

### Full-cycle gate

Do not consider the implementation complete until the following chain succeeds:

```text
Code change
    -> unit/integration tests
    -> container image build
    -> infrastructure what-if
    -> infrastructure deployment
    -> Application Insights connection-string injection
    -> application deployment
    -> health check
    -> authenticated transaction smoke test
    -> Log Analytics query by CorrelationId
    -> Application Insights query by app.correlation_id
    -> operation_Id trace inspection
```

The transaction smoke test must verify the same values across the response, Serilog event, Application Insights custom dimension, and broker metadata. A health check alone proves container availability; it does not prove observability configuration or end-to-end correlation.

## 8. Environment Policy

### Development

- Serilog writes to console and the local rolling file sink.
- OpenTelemetry console export is enabled by default.
- Serilog emits structured JSON output.
- Azure Monitor export is optional and should normally remain disabled unless a developer is intentionally testing cloud telemetry.
- No secrets or Application Insights connection strings are committed to JSON settings.

### Production

- Serilog writes to console only.
- Container Apps forwards console output to Log Analytics.
- Azure Monitor export is enabled by supplying `APPLICATIONINSIGHTS__CONNECTIONSTRING` through deployment configuration or a secret-backed environment value.
- The Application Insights connection string must not be committed to `appsettings.Production.json`.
- Key Vault-backed Container Apps secret configuration supplies `APPLICATIONINSIGHTS__CONNECTIONSTRING`.

Configuration precedence remains:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. environment variables
4. command-line arguments

## 9. Detailed Follow-On Implementation Plan

The following work is intentionally staged after the decisions above have been accepted.

### Implementation status

- [x] Step 1 request correlation middleware implemented and verified.
- [x] Step 2 API logging slice implemented and verified.
- [x] Mock Serilog JSON console logging implemented and verified.
- [x] Step 2 consumer event coverage implemented for RabbitMQ and Azure Service Bus consumers.
- [x] Step 3 telemetry naming and privacy checks applied to the current API event set.
- [x] Step 3 automated structured-property allowlist assertions implemented and verified.
- [x] Step 4 OpenTelemetry/Azure Monitor configuration complete for API and Mock.
- [x] Shared OpenTelemetry and Serilog startup registration extracted for API and Mock.
- [x] Step 5 idempotency repository/store event coverage for InMemory and Redis implementations.
- [x] Step 5 consumer retry/consume event coverage.
- [ ] Step 6 future EF Core data-layer integration.
- [x] Step 7 Azure Application Insights resource, Key Vault secret, and Container App secret propagation implemented in Bicep.
- [ ] Step 8 local and Azure end-to-end validation.

The next active implementation step is **Step 8: local and Azure end-to-end validation**. EF Core integration remains intentionally deferred until a real data layer and `DbContext` exist. Azure retention is configured at 5 days; sampling and the 100 MB/day cap still require deployment-time verification/configuration.

### Step 1: Establish one correlation enrichment point

Add request correlation enrichment in middleware or another request-wide component rather than only in the controller. The component should:

1. Resolve the application `CorrelationId` from the validated `Correlation-Id` header when supplied, and generate it from `HttpContext.TraceIdentifier` when absent.
2. Read the technical `TraceId` from `Activity.Current?.TraceId` and include it in the structured logging context when available.
3. Add the application value to `Activity.Current` as `app.correlation_id`. The OpenTelemetry Azure Monitor exporter will carry this value as a custom Application Insights property, commonly under `customDimensions`.
4. Add the same application value to the Serilog `LogContext` as `CorrelationId` so every log event emitted during the request carries it into Container Apps and Log Analytics.
5. Ensure the properties are removed when the request scope ends.

The implementation should be equivalent to the following request-scope behavior:

```csharp
var correlationId = string.IsNullOrWhiteSpace(context.TraceIdentifier)
    ? Guid.NewGuid().ToString("N")
    : context.TraceIdentifier;

var traceId = Activity.Current?.TraceId.ToString();
Activity.Current?.SetTag("app.correlation_id", correlationId);

using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("TraceId", traceId))
{
    await next(context);
}
```

The controller should consume this resolved correlation value instead of independently deriving it. This prevents request logs, telemetry, response data, and message envelopes from drifting apart.

### Step 2: Implement the source-generated structured logging contract

Replace ad hoc logger templates and the old custom logger abstraction with a shared `TransactionValidationLogger` event catalog in the common configuration boundary. Use typed `ILogger<TClass>` in every owning class; do not register a shared logger instance in dependency injection.

Apply the contract as follows:

1. [x] Convert `TransactionValidationLogger` to a `static partial` class.
2. [x] Implement the API events with `[LoggerMessage]`, stable `EventId` values, explicit levels, and fixed message templates.
3. [x] Pass the owning class's typed `ILogger<TClass>` to the shared generated methods so `SourceContext` identifies the owner.
4. [x] Use controller events for request receipt, validation/idempotency rejection, partner verification, publication, and processing failure.
5. [x] Add service events for partner verification outcomes.
6. [x] Add repository/data-access events for idempotency acquisition, cache lookup/storage, and release outcomes.
7. [x] Add messaging consumer retry, consume, and failure events for RabbitMQ and Azure Service Bus.
8. [x] Keep one owner for each implemented event so the same success is not emitted by every layer.
9. [x] Use individual typed parameters for generated methods and avoid logging payloads or unsafe values.
10. [x] Add focused logger tests that verify event IDs and structured property allowlists without asserting rendered message text.
11. [ ] Verify generated logger categories and JSON properties in local output.

### Step 3: Define telemetry naming and privacy rules

Use stable names for custom properties:

| Property | Meaning | Allowed value |
|---|---|---|
| `app.correlation_id` | Business/message correlation identifier | Non-sensitive identifier only |
| `app.message_id` | Accepted broker message identifier | Non-sensitive identifier only |
| `app.partner_id` | Partner identity used for the transaction | Only if approved as non-sensitive |
| `service.name` | OpenTelemetry service identity | `TransactionValidation` |

`IdempotencyKey` is intentionally excluded from structured logs because it may be supplied by a client and is not part of the approved Azure telemetry field set. Do not add request bodies, API keys, broker credentials, connection strings, raw idempotency keys, or transaction amounts to traces or general logs unless a separate data-classification decision approves them.

Step 3 verification checklist:

- [x] `CorrelationId`, `PartnerId`, `TransactionReference`, and `MessageId` remain the approved operational identifiers.
- [x] `IdempotencyKey` is excluded from generated log templates.
- [x] Request bodies, credentials, connection strings, raw SQL parameters, and financial values are excluded.
- [ ] Add automated assertions for the final JSON log property allowlist.

### Step 4: Complete OpenTelemetry configuration

Keep the existing typed `OpenTelemetryOptions` section and verify these behaviors:

1. ASP.NET Core instrumentation is enabled in all runtime environments.
2. HTTP client instrumentation is enabled in all runtime environments.
3. EF Core instrumentation is enabled and remains dormant until EF Core creates database operations.
4. Console export is used for local diagnostics.
5. Azure Monitor export is enabled only when the Application Insights connection string is present.
6. Production does not accidentally enable both an unwanted local file sink and cloud log duplication.

Current implementation status:

- [x] API ASP.NET Core and HTTP instrumentation are registered.
- [x] API EF Core instrumentation is registered and remains dormant until a `DbContext` exists.
- [x] Development uses the Console exporter.
- [x] Production explicitly selects `AzureMonitor`, preventing accidental Console export in Azure.
- [x] Azure Monitor activation remains conditional on a configured connection string.
- [x] Add the same OpenTelemetry registration and environment settings to the Mock service, as required by the final telemetry scope decision.

### Step 5: Complete EF Core integration in a future data-layer implementation

EF Core instrumentation is prepared in the current observability configuration, but this repository does not yet contain a `DbContext` or an EF Core data layer. The following work is intentionally deferred until that data layer is introduced:

1. Add the appropriate EF Core provider and `DbContext` registration.
2. Keep `OpenTelemetry.Instrumentation.EntityFrameworkCore` at a compatible version.
3. Confirm the existing `AddEntityFrameworkCoreInstrumentation()` registration remains present once in the shared telemetry pipeline.
4. Exercise a real query in an integration test.
5. Verify the resulting database span contains duration, success/failure, and safe database metadata.
6. Confirm sensitive SQL parameter values are not exported.

### Step 7: Add Azure deployment wiring

The Azure deployment must provide `APPLICATIONINSIGHTS__CONNECTIONSTRING` through a secret-backed mechanism. The implementation should:

1. Provision or reference the Application Insights resource and its connection string.
2. Store the connection string as a deployment secret or secure parameter.
3. Inject it into the API Container App environment using the standard double-underscore configuration key.
4. Keep the Mock service decision explicit: enable Azure telemetry only if its operational traces are required.
5. Document the required role, secret, and workspace relationships.

Current implementation status:

- [x] Provision workspace-based Application Insights linked to the environment Log Analytics workspace.
- [x] Store the Application Insights connection string in Key Vault.
- [x] Grant both Container App identities Key Vault Secrets User access.
- [x] Inject `APPLICATIONINSIGHTS__CONNECTIONSTRING` into both API and Mock Container Apps.
- [x] Configure Log Analytics retention at 5 days.
- [ ] Verify/configure 10% sampling and the 100 MB/day Application Insights ingestion cap with an 80% alert in Azure.

### Step 8: Validate locally and in Azure

Local validation should confirm:

- API request spans are printed by the Console exporter.
- Outbound partner verification spans are children of the API request span.
- Serilog events include `CorrelationId` after the enrichment change.
- The API response and broker envelope retain the same application `CorrelationId`.
- No secrets or sensitive payload values appear in logs or telemetry.

Azure validation should confirm:

- Container logs are visible in Log Analytics.
- Application Insights shows request and dependency telemetry.
- `operation_Id` groups the technical trace.
- `app.correlation_id` finds the related business transaction.
- JSON log properties preserve the owning class category and stable `EventId`.
- A request failure can be investigated from both the trace and its surrounding logs.

### Application Insights query

Search for the business correlation value in OpenTelemetry telemetry. The exact custom-property column can vary by Application Insights table and workspace mode, so inspect both common representations when validating the deployment:

```kusto
union AppRequests, AppDependencies, AppTraces, AppExceptions
| where tostring(customDimensions["app.correlation_id"]) == "correlation-value"
    or tostring(Properties["app.correlation_id"]) == "correlation-value"
| project timestamp, itemType, name, operation_Id, operation_ParentId, customDimensions, Properties, success, duration
| order by timestamp asc
```

After finding the `operation_Id`, inspect the complete technical trace:

```kusto
union AppRequests, AppDependencies, AppTraces, AppExceptions
| where operation_Id == "trace-id"
| project timestamp, itemType, name, operation_Id, operation_ParentId, customDimensions, success, duration
| order by timestamp asc
```

### Log Analytics query

Search Container Apps console output for the Serilog correlation property. The table and parsed property names depend on the workspace table mode, so validate the deployed schema before standardizing the final query:

```kusto
ContainerAppConsoleLogs
| where Log_s contains "CorrelationId=correlation-value"
| project TimeGenerated, ContainerAppName, RevisionName, ContainerName, Log_s
| order by TimeGenerated asc
```

If structured properties are parsed into columns:

```kusto
ContainerAppConsoleLogs
| where tostring(Properties.CorrelationId) == "correlation-value"
| project TimeGenerated, ContainerAppName, Properties, Log
| order by TimeGenerated asc
```

If the workspace uses the legacy diagnostics table:

```kusto
AzureDiagnostics
| where Category == "ContainerAppConsoleLogs"
| where Message contains "CorrelationId=correlation-value"
| project TimeGenerated, Resource, Message
| order by TimeGenerated asc
```

The investigation sequence is therefore:

1. Start with the client-facing `CorrelationId`.
2. Find the related Serilog events in Log Analytics.
3. Find `app.correlation_id` in Application Insights custom dimensions.
4. Read `operation_Id` from the matching Application Insights request.
5. Query that `operation_Id` to inspect HTTP and future EF Core dependency spans.

## 10. Example Investigation Workflow

Given a client response containing `correlationId`:

1. Search Log Analytics for the application correlation property in Container Apps logs.
2. Search Application Insights custom dimensions for `app.correlation_id`.
3. Open the matching request telemetry and use its `operation_Id`/`TraceId` to inspect child dependency spans.
4. Check the partner verification span for timeout, status, and duration.
5. Check the broker log or consumer observation using the same application `CorrelationId`.

This workflow deliberately uses both identifiers: the business ID finds the transaction, and the trace ID explains the technical execution path.

## 11. Acceptance Criteria

The observability implementation is complete when:

- Production Serilog logs are available through Container Apps and Log Analytics.
- Application Insights receives OpenTelemetry requests, HTTP dependencies, and metrics when configured.
- The future EF Core query is represented as an Application Insights dependency span.
- `TraceId` and application `CorrelationId` are both available and not confused with one another.
- The application `CorrelationId` is present in API responses, broker metadata, logs, and Application Insights custom dimensions.
- Every application event uses a source-generated `[LoggerMessage]` definition with a stable `EventId` and typed structured parameters.
- Every emitting class uses `ILogger<TClass>`, and Log Analytics can identify the owning class through the logging category.
- No per-class private wrapper methods or `ITransactionValidationLogger : ILogger` abstraction is required.
- Application Insights and Log Analytics do not receive avoidable duplicate copies of the same log stream.
- Development and Production settings have documented, testable differences.
- Automated or integration validation covers exporter configuration, correlation enrichment, and the Azure connection-string path.

## Related Documentation

- [Azure Deployment Plan](../implementation/azure_deployment_plan.md)
- [Architecture Design](../architecture_design/Architecture_design.md)
- [Implementation Checklist](../implementation/implementation_checklist.md)
- [API Idempotency and Correlation Semantics](../architecture_design/api_idempotency_flow_and_semantics.md)