# Coding Standards

This document defines reusable .NET coding principles and records the implementation patterns evidenced in this repository. Repository examples are reference patterns, not universal requirements: apply a pattern when its applicability conditions hold, and distinguish repository-specific constraints from general engineering guidance.

## Purpose

The repository follows a pragmatic service-oriented design with separately composed executable hosts, explicit project boundaries, and dependency inversion. Code should be easy to reason about, easy to test, and explicit about where infrastructure decisions are made.

The standards below are not theoretical; they reflect the actual implementation model used across the API, configuration, messaging, integration, and mock host projects.

## Core engineering principles

### 1. Keep the executable host thin and explicit

The `Program` entry point is the composition root. It composes configuration, middleware, validation, health checks, observability, and selected infrastructure services. It does not contain business rules or broker-specific domain logic.

Observed pattern in the repo:

- `TransactionValidation.Api/Program.cs` registers middleware, health checks, Swagger, `AddTransactionValidationCommonServices`, and a selected broker.
- `TransactionValidation.Mock/Program.cs` follows the same host composition pattern.

Reusable rule:

- executable entry points should wire dependencies, not implement business logic
- application behavior belongs to the project or service that owns the concern
- keep startup code easy to trace and low in branching logic

### 2. Program to abstractions, not runtime transport details

The repository uses interfaces for runtime contracts that are intentionally stable across broker implementations.

Examples from the codebase:

- `IMessagePublisher` defines the business-facing publishing contract
- `IIdempotencyStore` defines the state contract for duplicate detection and replay
- `IPartnerVerifier` abstracts the external verification dependency

The concrete implementations are registered in the composition root rather than in the core domain layer.

Reusable rule:

- keep contracts in a shared application/core layer
- isolate infrastructure decisions behind implementation-specific classes
- allow runtime selection by configuration without modifying domain logic

### 3. Prefer typed options over ad hoc environment reading

The configuration project contains strongly typed option classes and binds them via `IOptions<T>`.

Examples:

- `ApiKeyOptions`
- `IdempotencyOptions`
- `PartnerVerificationOptions`
- `RabbitMqOptions`
- `ServiceBusPublisherOptions`
- `RedisOptions`
- `OpenTelemetryOptions`

This pattern keeps configuration explicit, strongly typed, and testable.

Reusable rule:

- bind configuration to dedicated option classes instead of reading environment variables directly in business logic
- separate environment configuration from business rules
- treat defaults and validation as configuration concerns, not runtime behavior

### 4. Use middleware and exception handling as cross-cutting infrastructure

The repository centralizes request-level concerns in middleware and exception handling rather than scattering them across controllers.

Examples:

- `ApiKeyMiddleware` validates the incoming API key before the request reaches the controller
- `CorrelationContextMiddleware` ensures a request context exists for logs and response correlation
- `ApiExceptionHandler` converts domain and infrastructure exceptions to RFC 7807 `ProblemDetails`

Reusable rule:

- keep validation, correlation, logging context, and error normalization outside the controller business flow
- map infrastructure and domain failures to consistent API responses
- preserve the controller as the orchestration point, not the global policy layer

### 5. Keep orchestration explicit and async-first

The controller is the orchestrator, not the data access or broker implementation itself. It coordinates validation, idempotency, partner verification, and publication through abstractions.

Examples:

- `PartnerTransactionsController.CreateAsync` performs validation, acquires an idempotency slot, verifies the partner, publishes to the broker, and returns the accepted response
- async methods accept `CancellationToken` and the workflow propagates cancellation through the request path

Reusable rule:

- controllers and services should orchestrate behavior using async flows and cancellation tokens
- avoid mixing domain execution with transport-specific code
- do not hide external network calls or retries inside the controller’s request path without a shared abstraction

### 6. Idempotency should be modeled as an explicit contract, not as ad hoc checks

This repo treats idempotency as a first-class runtime behavior.

The implementation includes:

- `IIdempotencyStore` as the contract
- `TryAcquire` for duplicate/conflict detection
- `TryGetCachedResponse` for accepted-response replay
- `StoreCachedResponse` for replay-safe accepted states
- `Release` for failed processing recovery

The API distinguishes between:

- duplicate payload replay
- same key with different payload
- fresh accepted transaction

Reusable rule:

- define idempotency as a clear contract with explicit acquisition/replay semantics
- make replay behavior deterministic and bounded by time-to-live
- reject conflicting payloads for the same logical request rather than silently accepting them

### 7. Favor sealed, small, explicit types for implementation leaves

The repo uses `sealed` classes for concrete implementations and narrow responsibilities. This keeps inheritance shallow and reduces unintended extension points.

Examples:

- `PartnerTransactionsController`
- `ApiExceptionHandler`
- `RabbitMqMessagePublisher`
- `ServiceBusMessagePublisher`
- `InMemoryIdempotencyStore`
- `RedisIdempotencyStore`

Reusable rule:

- prefer sealed concrete classes unless extension is an explicit requirement
- avoid broad inheritance hierarchies for infrastructure or service implementations
- use small classes with one clear reason to change

### 8. Use source-generated logging instead of ad hoc `ILogger` extension calls

The codebase uses `LoggerMessage` methods in the `TransactionValidation.Core/Logging` directory.

Examples:

- `TransactionValidationLogging`
- `MessagingLogging`
- `RabbitMqLogging`
- `ServiceBusLogging`

This provides compile-time message generation, stable event IDs, and structured logging contracts.

Reusable rule:

- prefer source-generated logging for production code
- keep event IDs stable and owned by the relevant catalog
- log structured data, not secrets or raw sensitive payloads
- separate event ownership from the call site

### 9. Keep service health and runtime diagnostics part of the contract

The API exposes health checks and uses them as a runtime signal rather than a one-off debug tool.

Examples:

- `MessagingHealthCheck`
- `RedisHealthCheck`
- `app.MapHealthChecks("/healthz")`

Reusable rule:

- expose health endpoints for key runtime dependencies
- keep health probes lightweight and deterministic
- treat health status as operational evidence, not a substitute for end-to-end validation

### 10. Validate with the real boundaries that matter

The repo uses unit tests for behavior, integration-host tests for startup and host composition, and E2E tests for runtime topology. This is not just a coverage exercise; it is a separation of concerns for validation.

Reusable rule:

- validate behavior at the correct boundary
- keep host startup checks separate from business logic unit tests
- use integration and E2E tests to prove composition, topology, and runtime contracts

## Applicable pattern rules

The following catalog turns those principles into reviewable implementation rules. A requirement using **MUST/MUST NOT** is mandatory when its applicability conditions hold; other core principles are guidance. These rules are written as reusable .NET guidance; apply them only when their stated applicability conditions hold. The examples are illustrative C# shapes, not drop-in implementations; adapt types, libraries, and failure semantics to the project. TransactionValidation-specific evidence and concrete domain implementations remain in the cited feature guides.

Every mandatory rule in this catalog MUST use the following fields so contributors and reviewers can apply it consistently:

- **Rule ID and title** - stable identifier plus concise name; do not reuse an ID for a different requirement.
- **Applicability** - conditions under which the rule applies.
- **Requirement** - precise MUST/MUST NOT behavior.
- **Rationale** - risk or inconsistency the rule prevents.
- **Approved pattern** - concise implementation shape or link to the canonical pattern.
- **Code example** - a small illustrative C# shape for the reusable pattern; it must not imply a particular domain, broker, or persistence technology is mandatory.
- **Verification** - test, analyzer, build check, or review evidence that demonstrates compliance.
- **Exception process** - the applicable approval and recording path; an exception does not waive protected security, privacy, or data-integrity constraints.

### CS-EXT-01 — Keep external protocol and SDK details inside adapters

- **Applicability** - a feature calls an external HTTP API, broker, cache, or SDK.
- **Requirement** - application-facing code MUST depend on a stable application contract. The adapter MUST own protocol construction, SDK-specific metadata mapping, terminal outcome translation, and the lifecycle of resources it creates or owns.
- **Rationale** - application orchestration should not change when a transport or SDK implementation changes.
- **Avoid** - exposing `HttpResponseMessage`, broker SDK message types, queue names, or transport-specific exceptions to domain/application workflows; creating unowned resources per operation.
- **Approved pattern** - implement a transport-specific adapter behind an application-owned interface; see [reliability](../../features/reliability/README.md) and [messaging](../../features/messaging/README.md). Repository examples include `IPartnerVerifier` and `IMessagePublisher`.
- **Code example** - see the illustrative adapter shape below.
- **Verification** - focused adapter tests prove protocol/outcome translation and resource behavior; host-level tests prove contract registration.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative C# shape:**

```csharp
public interface IProfileClient
{
    Task<Profile?> GetAsync(string profileId, CancellationToken cancellationToken);
}

public sealed class ProfileHttpClient : IProfileClient
{
    private readonly HttpClient _httpClient;

    public ProfileHttpClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<Profile?> GetAsync(string profileId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"profiles/{Uri.EscapeDataString(profileId)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Profile>(cancellationToken);
    }
}
```

### CS-RES-01 — Centralize dependency resilience and preserve cancellation

- **Applicability** - a feature makes network or remote-service calls.
- **Requirement** - the dependency-client boundary MUST define bounded timeout and resilience behavior. Retries MUST be limited to operations and failure classes that are safe to retry; attempt/overall budgets and circuit breaking MUST be configured there when required by the dependency risk. Caller cancellation MUST propagate; dependency timeout and terminal protocol outcomes MUST be translated at the adapter into the application's established outcome categories.
- **Rationale** - one bounded policy is easier to reason about than hidden or duplicated retries, and cancellation must remain distinguishable from a dependency timeout.
- **Avoid** - controller retry loops, nested retry policies without an explicit budget, unbounded waits, or translating caller cancellation into a dependency failure.
- **Approved pattern** - configure the typed HTTP client resilience pipeline and translate terminal outcomes in the client adapter; see [reliability](../../features/reliability/README.md).
- **Code example** - see the illustrative client-pipeline shape below.
- **Verification** - resilience retry, timeout guardrail, cancellation, and adapter outcome tests; see the verification surface in the reliability guide.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative .NET client-pipeline shape:**

```csharp
services.AddHttpClient<IProfileClient, ProfileHttpClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 2;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
    });

try
{
    return await client.GetAsync(profileId, cancellationToken);
}
catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    throw new DependencyTimeoutException("Profile service timed out.");
}
```

### CS-CFG-01 — Bind and validate configuration at the composition boundary

- **Applicability** - a feature introduces deployment- or environment-dependent settings.
- **Requirement** - settings MUST have an owning typed options model, documented defaults/required values, and validation for required values, ranges, and incompatible combinations. Runtime selection MUST fail clearly when a configured implementation is unsupported or incomplete. Defaults MUST NOT silently disable a required safety property.
- **Rationale** - invalid runtime configuration should be discoverable before feature execution, while domain logic stays independent of environment variables and raw configuration keys.
- **Avoid** - scattered string-key reads in business code, undocumented fallback behavior, or silently registering an unintended implementation.
- **Approved pattern** - bind a named options type and validate its required values and cross-setting constraints during startup/registration; see [configuration and environment](../configuration/configuration_and_environment.md) and [broker registration](../../features/messaging/README.md).
- **Code example** - see the illustrative options-validation shape below.
- **Verification** - test valid, missing, invalid, and boundary values; verify unsupported selections fail during composition.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative options-validation shape:**

```csharp
services.AddOptions<ProfileServiceOptions>()
    .BindConfiguration(ProfileServiceOptions.SectionName)
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "BaseUrl must be an absolute URI.")
    .ValidateOnStart();
```

### CS-CFG-02 — Keep secrets out of source-controlled configuration

- **Applicability** - an application needs credentials, tokens, connection strings, certificates, or other secret values.
- **Requirement** - real secrets MUST NOT be committed in source, appsettings files, launch profiles, tests, or documentation. Applications MUST receive secrets from an appropriate runtime secret source; local development SHOULD use a developer-local secret store or untracked environment file. Secret values MUST NOT be logged.
- **Rationale** - source control, build artifacts, and diagnostics have wider persistence and access than runtime secret stores.
- **Approved pattern** - bind secret-bearing settings to typed options as usual, but supply values via .NET user secrets for local development or the deployment platform's secret provider/identity integration in hosted environments.
- **Code example** - the following shows local user-secrets loading and typed binding; production secret-provider registration is host/platform-specific.

```csharp
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddOptions<PartnerClientOptions>()
        .BindConfiguration(PartnerClientOptions.SectionName)
        .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Partner API key is required.")
        .ValidateOnStart();
```

- **Verification** - inspect committed settings and launch profiles for real credentials; verify missing required secrets fail at startup and logs do not expose their values.
- **Exception process** - secret exposure is not waivable; document an approved alternate secret-delivery design and its security review using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

### CS-DEBUG-01 — Keep local debug profiles safe and reproducible

- **Applicability** - a .NET project provides IDE or command-line launch profiles for local debugging.
- **Requirement** - debug profiles MUST identify the intended startup target and non-production environment, and MUST NOT contain real secrets or production credentials. Profiles SHOULD define required local URLs/settings explicitly so debugging is repeatable without changing production code.
- **Rationale** - reproducible profiles reduce setup errors while preventing local tooling configuration from becoming a credential store or production behavior switch.
- **Approved pattern** - keep non-secret launch settings in the project profile; load credentials separately from user secrets or another local secret provider. This example is scoped to ASP.NET Core `launchSettings.json`; other .NET hosts use their IDE's equivalent profile format.
- **Code example** - launch profile configuration (not a production setting):

```json
{
    "profiles": {
        "Api": {
            "commandName": "Project",
            "launchBrowser": false,
            "applicationUrl": "https://localhost:7001;http://localhost:5000",
            "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        }
    }
}
```

- **Verification** - start the documented profile and verify the intended project, environment, and listener; review the committed profile for secrets and production endpoints.
- **Exception process** - record the Rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record); secrets MUST NOT be excepted into source control.

### CS-FLOW-01 — Validate before side effects and make state transitions explicit

- **Applicability** - request processing can reserve state, call dependencies, or publish/persist data.
- **Requirement** - input validation MUST precede side effects. The workflow MUST make acquisition, success, replay/conflict, and failure-release transitions explicit. Cache or persist a successful result only after the success condition that justifies it.
- **Rationale** - invalid input and failed work must not leave misleading state or trigger downstream work.
- **Avoid** - acquiring durable/in-flight state before validation, representing failure as accepted success, or leaving acquired state stranded after exceptions.
- **Approved pattern** - validate before acquisition; cache only accepted outcomes; release acquired state when downstream processing fails. Detailed semantics are owned by [validation and security](../../features/validation-and-security/README.md), [reliability](../../features/reliability/README.md), and [distributed state](../../features/distributed-state/README.md).
- **Code example** - see the illustrative workflow shape below.
- **Verification** - test validation-before-side-effects and success, conflict, replay, and release behavior at the owning boundary.
- **Scope note** - idempotency defines request/data replay semantics; retry, timeout, and recovery define operational failure handling. Do not collapse these into one policy.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative workflow shape:**

```csharp
await validator.ValidateAndThrowAsync(request, cancellationToken);

var acquisition = await store.TryAcquireAsync(request.Key, cancellationToken);
if (acquisition.IsDuplicate)
{
    return acquisition.CachedResult;
}

try
{
    var result = await dependency.ExecuteAsync(request, cancellationToken);
    await store.StoreSuccessAsync(request.Key, result, cancellationToken);
    return result;
}
catch
{
    await store.ReleaseAsync(request.Key, cancellationToken);
    throw;
}
```

### CS-HOST-01 — Keep executable hosts thin and explicit

- **Applicability** - a .NET application has an executable host or entry point that composes runtime services.
- **Requirement** - executable entry points MUST configure the host, register dependencies, compose middleware/routes or hosted services, and start the application. Business workflows MUST remain in the components that own them; host code MUST NOT become the implementation home for feature behavior.
- **Rationale** - a visible composition boundary keeps runtime choices reviewable and prevents startup code from becoming coupled to domain behavior.
- **Avoid** - embedding business decisions in `Program`, duplicating host configuration across entry points, or putting reusable feature behavior in startup extensions without an owned responsibility.
- **Approved pattern** - keep composition at the host boundary and move repeatable registration into focused, owner-specific extensions. The portable example is in the [reusable pattern section](#reusable-patterns-derived-from-this-repository).
- **Code example** - see the CS-HOST-01 composition-root pattern in the reusable pattern section.
- **Verification** - build and start the host; use host integration tests to verify service registration, middleware ordering, endpoint mapping, and selected runtime implementations.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

### CS-HTTP-01 — Normalize ASP.NET Core errors with ProblemDetails

- **Applicability** - an ASP.NET Core HTTP API translates application or dependency failures into HTTP responses.
- **Requirement** - known application exception categories MUST map to stable HTTP status/title/type values in `ProblemDetails`; each recognized exception MUST use its specific message as `Detail`. Unknown exceptions MUST return a generic `500` `ProblemDetails` without internal exception details. Middleware-generated failures MUST use the same representation when they are part of the API's error contract.
- **Rationale** - clients receive a consistent machine-readable error shape while useful known failure context is preserved and implementation details remain private.
- **Avoid** - constructing different ad hoc error payloads in controllers, returning exception stack traces, or using an exception's raw message for an unrecognized `500`.
- **Approved pattern** - centralize exception mapping in ASP.NET Core `IExceptionHandler`; see the [ProblemDetails pattern and code example](#reusable-patterns-derived-from-this-repository).
- **Code example** - see the CS-HTTP-01 ProblemDetails pattern in the reusable pattern section.
- **Verification** - unit tests MUST assert mapped status, content type, and exception-specific `Detail`; host tests SHOULD verify the same contract through the real pipeline.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

### CS-MSG-01 — Design message consumers for independent at-least-once delivery

- **Applicability** - a feature publishes or consumes broker messages.
- **Requirement** - publisher acceptance MUST NOT be described as consumer completion. When fan-out is required, each consumer MUST have an independent delivery path. For manually acknowledged/completed consumers, acknowledgement MUST follow successful required processing; if automatic acknowledgement is selected, its delivery-loss semantics MUST be explicit and justified. Processing MUST account for possible redelivery and use consumer-owned deduplication when side effects are not naturally idempotent.
- **Rationale** - broker acceptance, routing, consumer processing, and acknowledgement are separate events; a failure before acknowledgement can cause redelivery.
- **Avoid** - assuming one publish means one successful processing, sharing a competing-consumer destination when fan-out is required, or assuming API idempotency prevents consumer redelivery.
- **Approved pattern** - publish once through the transport-neutral contract and give each fan-out consumer its own queue/subscription; see [messaging](../../features/messaging/README.md) and [messaging topology and consumer routing](../../architecture/messaging_topology_and_consumer_routing.md).
- **Code example** - see the illustrative consumer shape below.
- **Verification** - verify routing/fan-out, acknowledgement timing, and redelivery at the appropriate integration/runtime boundary.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative consumer shape:**

```csharp
var delivery = await receiver.ReceiveAsync(cancellationToken);
var message = deserializer.Deserialize(delivery.Body);

await handler.HandleAsync(message, cancellationToken);
await delivery.AcknowledgeAsync(cancellationToken);
```

### CS-OBS-01 — Propagate context while minimizing sensitive telemetry

- **Applicability** - a feature crosses request, service, or message boundaries or emits logs/telemetry.
- **Requirement** - preserve the relevant correlation/message identity across boundaries using structured fields. Emit only operationally necessary data; secrets, credentials, raw payloads, and sensitive business values MUST NOT be logged.
- **Rationale** - distributed work must be diagnosable without turning telemetry into a sensitive-data store.
- **Avoid** - inventing unrelated IDs in each layer, logging whole request objects, or using raw credentials/idempotency keys as diagnostic fields.
- **Approved pattern** - propagate correlation/message identity as structured fields through request scope, trace, envelope, and broker metadata without logging sensitive values; see [observability](../../features/observability/README.md), [validation and security](../../features/validation-and-security/README.md), and [logging standards](../../operations/diagnostics/logging_standards.md).
- **Code example** - see the illustrative structured-logging shape below.
- **Verification** - use correlation/metadata tests and privacy analyzers or source checks to verify propagation and sensitive-field exclusions.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative structured-logging shape:**

```csharp
public static partial class RequestLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Request accepted. CorrelationId={CorrelationId}")]
    public static partial void Accepted(ILogger logger, string correlationId);
}

Activity.Current?.SetTag("app.correlation_id", correlationId);
RequestLog.Accepted(logger, correlationId); // Do not add credentials or raw payloads.
```

### CS-TEST-01 — Test observable behavior at the owning boundary

- **Applicability** - adding or changing feature behavior.
- **Requirement** - unit tests MUST prove owned decision logic; host/integration tests MUST prove relevant composition and middleware behavior; runtime tests SHOULD prove infrastructure topology or protocol behavior when the requirement depends on it. Failure paths and invariants MUST be covered, not only the happy path.
- **Rationale** - confidence should come from the boundary that owns the behavior, while avoiding slow infrastructure tests for logic that can be tested deterministically.
- **Avoid** - asserting only mock interactions when an observable result can be tested, using E2E as the sole coverage for business logic, or adding test-only production APIs to simplify assertions.
- **Approved pattern** - verify owned decisions with focused tests, host composition at the host boundary, and infrastructure contracts with runtime tests only when required; see [testing and quality](../../features/testing-and-quality/README.md).
- **Code example** - see the illustrative behavior-focused unit-test shape below.
- **Verification** - follow [quality policy](../quality/quality_policy.md) and use the repository's [test execution guide](../../operations/testing/test/test_execution_and_coverage_guide.md) for supported commands.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative behavior-focused unit-test shape:**

```csharp
[Fact]
public async Task HandleAsync_WhenRequestIsValid_ReturnsAcceptedResult()
{
    var result = await _sut.HandleAsync(_validRequest, CancellationToken.None);

    Assert.Equal(ProcessingStatus.Accepted, result.Status);
}
```

### CS-TEST-02 — Make failure scenarios deterministic and safely scoped

- **Applicability** - a feature's correctness depends on observing a failure, timeout, retry, cancellation, redelivery, or recovery path that is rare, timing-sensitive, or controlled by an external dependency.
- **Requirement** - tests MUST be able to trigger the relevant failure deterministically through a test double, test-host seam, or dedicated mock/runtime control. Such controls MUST be explicit, narrowly scoped to the scenario, and isolated from the production application boundary. Randomized failure simulation MAY supplement deterministic tests but MUST NOT be the sole evidence for a required failure invariant.
- **Rationale** - nondeterministic failures make regressions difficult to reproduce and can leave important recovery behavior unverified.
- **Approved pattern** - inject a deterministic dependency outcome for unit/host tests; for runtime broker behavior, use an explicit mock-host control keyed to a message/scenario and trigger failure at the intended boundary (for example, immediately before acknowledgement). Keep test observation and failure-control endpoints out of the production API; see [testing and quality](../../features/testing-and-quality/README.md), [reliability](../../features/reliability/README.md), and [messaging](../../features/messaging/README.md).
- **Code example** - see the illustrative deterministic-failure test shape below.
- **Verification** - prove the test can reliably observe the expected failure and recovery outcome; use the owning unit, host-integration, or runtime test category. Confirm test-only controls are registered/exposed only by the mock or test host.
- **Exception process** - document the rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

**Illustrative deterministic-failure test shape:**

```csharp
var dependency = new StubProfileClient(_ =>
    Task.FromException<Profile?>(new TimeoutException("Simulated timeout.")));
var sut = new ProfileService(dependency);

await Assert.ThrowsAsync<TimeoutException>(
    () => sut.LoadAsync("profile-123", CancellationToken.None));
```

### CS-TEST-03 — Exercise host integration boundaries without replacing the host

- **Applicability** - a feature must verify service registration, middleware, routing, framework hosting, or cross-component composition in a hosted .NET application.
- **Requirement** - host integration tests MUST execute the actual application host and its relevant pipeline. Tests MAY replace external or nondeterministic dependencies at stable contracts; they MUST NOT replace the host pipeline or the behavior under test. ASP.NET Core applications SHOULD use `WebApplicationFactory<TEntryPoint>` when it fits the hosting model.
- **Rationale** - host-level defects often occur in composition and ordering that isolated unit tests cannot observe.
- **Avoid** - rebuilding a parallel test-only host that omits production registration, replacing the middleware being tested, or using live external services when a stable deterministic substitute suffices.
- **Approved pattern** - start the real host, replace only external dependencies, issue a real in-process HTTP request, and assert the observable response or state.
- **Code example** - ASP.NET Core host-integration shape:

```csharp
[Trait("Category", "Integration")]
[Fact]
public async Task Request_WhenBoundaryCondition_ReturnsExpectedResponse()
{
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRemoteGateway>();
            services.AddSingleton<IRemoteGateway>(new DeterministicRemoteGateway());
        }));
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/resource");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

- **Verification** - run the integration category and assert the response, middleware/registration behavior, and any relevant state transition. Test setup SHOULD be deterministic.
- **Exception process** - document the Rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

### CS-TEST-04 — Reserve E2E tests for critical runtime contracts

- **Applicability** - correctness depends on deployed/container topology, network configuration, real infrastructure protocols, or multiple independently running services that lower-level tests cannot prove.
- **Requirement** - E2E tests MUST exercise externally observable behavior across the required runtime boundary and MUST state their infrastructure/environment prerequisites. When the scenario exercises repository-owned multi-service topology and Docker Compose can provide the required dependencies, E2E MUST use Compose and real service/network paths; dependencies relevant to the claim MUST NOT be replaced with mocks. E2E SHOULD cover a small set of high-value critical paths; unit and integration tests remain responsible for detailed edge cases. Tests MUST avoid claiming CI or release-gate status unless an actual workflow enforces them.
- **Rationale** - real runtime tests catch deployment and topology faults, but are slower and more environment-sensitive than focused tests.
- **Avoid** - using E2E as the only test level, duplicating every unit case in E2E, relying on uncontrolled random failures, replacing the topology under test with mocks, or reporting an E2E suite as an automated gate when it is only locally runnable.
- **Approved pattern** - prefer the repository's Docker Compose topology for local multi-service E2E: start the required services, submit a real request over HTTP, then assert the response and critical downstream effect. Use a deployed/external environment when the scenario specifically tests cloud-managed behavior unavailable locally, and document its prerequisites. See [Compose and repository configuration rules](../configuration/repository_configuration_reference.md#rcfg-compose-01-use-compose-as-the-local-multi-service-e2e-topology).
- **Code example** - runtime smoke-test shape; fixture setup and teardown belong to the owning test harness:

```csharp
[Trait("Category", "E2E")]
[Fact]
public async Task AcceptedRequest_ReachesRuntimeDependency()
{
    using var client = _runtimeFixture.CreateApiClient();
    using var response = await client.PostAsJsonAsync("/requests", _validRequest);
    var accepted = await response.Content.ReadFromJsonAsync<AcceptedResponse>();

    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    Assert.NotNull(accepted);
    Assert.True(await _runtimeFixture.DependencyObservedAsync(accepted!.MessageId));
}
```

- **Verification** - run the E2E category against its declared runtime prerequisites; confirm startup, routing, critical downstream behavior, and cleanup. Keep E2E results separate from unit/integration results.
- **Exception process** - document the Rule ID, reason, impact, approver, review condition, and compensating verification using the [feature-delivery exception record](../feature_delivery_workflow.md#exception-record).

## Reusable patterns derived from this repository

These patterns were distilled from the implementation and are intended for reuse when their stated scope and applicability fit another project. “Repository-derived” describes their provenance, not a restriction to TransactionValidation. The rule catalog above remains authoritative for mandatory requirements; the examples here are adaptable shapes, not complete drop-in implementations. Domain feature guides link to concrete code and tests showing how this repository applies each pattern.

### Pattern: CS-HOST-01 — Keep executable hosts thin and explicit

- **Scope** - ASP.NET Core web hosts.
- **Applies when** - an executable must assemble configuration, application services, middleware, hosted services, or endpoints.
- **Reusable shape** - the host selects and composes dependencies; domain behavior remains in services that own the behavior.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RemoteServiceOptions>()
    .BindConfiguration(RemoteServiceOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddApplicationServices();

var app = builder.Build();
app.UseApplicationMiddleware();
app.MapApplicationEndpoints();
app.Run();
```

The registration and middleware methods are placeholders for the consuming application. Do not copy TransactionValidation-specific registration names. For repository code evidence, see [the API and Mock host composition pattern](../../features/runtime-and-api/README.md#pattern-thin-executable-composition-root) and [broker selection at composition time](../../features/messaging/README.md#pattern-select-one-broker-at-composition-time).

### Pattern: CS-EXT-01 — Keep external protocol and SDK details inside adapters

- **Scope** - general .NET application/integration boundary.
- **Applies when** - application behavior depends on an external protocol, SDK, or infrastructure implementation that should not leak into feature orchestration.
- **Reusable shape** - define the application-facing contract, then implement it in an adapter that owns protocol details and resource behavior.

The portable example is the [CS-EXT-01 adapter example](#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters). Repository code evidence includes [transport-neutral message publication](../../features/messaging/README.md#pattern-publish-through-a-transport-neutral-contract) and [partner outcome translation at the adapter](../../features/reliability/README.md#pattern-translate-terminal-dependency-outcomes-at-the-adapter).

### Pattern: CS-FLOW-01 — Validate before side effects and make state transitions explicit

- **Scope** - distributed/request processing; conditional, not a default requirement for every API.
- **Applies when** - callers may retry a request after uncertainty about completion and the system must suppress duplicate side effects or replay an accepted result.
- **Reusable shape** - define stable request identity and payload consistency, acquire state atomically, replay only a completed success, and release an acquired reservation when processing fails.

The portable workflow example is the [CS-FLOW-01 state-transition example](#cs-flow-01-validate-before-side-effects-and-make-state-transitions-explicit). Repository code evidence includes [the shared idempotency workflow contract](../../features/distributed-state/README.md#pattern-store-behind-one-workflow-contract), [bounded accepted-response replay](../../features/distributed-state/README.md#pattern-bounded-accepted-response-replay), and [release after downstream failure](../../features/reliability/README.md#pattern-release-request-reservation-after-downstream-failure).

### Pattern: CS-HTTP-01 — Normalize ASP.NET Core errors with ProblemDetails

- **Scope** - ASP.NET Core HTTP APIs.
- **Applies when** - an ASP.NET Core API needs a stable error contract across middleware, controllers, and application services.
- **Reusable shape** - translate each known application exception at the HTTP boundary into `ProblemDetails` with its mapped status/title/type and the exception's specific `Message` as `Detail`. Unexpected failures MUST produce a generic `500` `ProblemDetails` with a safe fixed detail, never the internal exception message. Middleware-generated errors MUST use the same representation when they participate in the same API error contract. Non-HTTP .NET hosts should use the equivalent error representation for their transport.

```csharp
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            BadRequestException error => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = error.Message,
                Type = "https://httpstatuses.com/400"
            },
            NotFoundException error => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = error.Message,
                Type = "https://httpstatuses.com/404"
            },
            ConflictException error => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = error.Message,
                Type = "https://httpstatuses.com/409"
            },
            UnauthorizedAccessException error => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = error.Message,
                Type = "https://httpstatuses.com/401"
            },
            UpstreamTimeoutException error => new ProblemDetails
            {
                Status = StatusCodes.Status408RequestTimeout,
                Title = "Request Timeout",
                Detail = error.Message,
                Type = "https://httpstatuses.com/408"
            },
            UpstreamServiceUnavailableException error => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service Unavailable",
                Detail = error.Message,
                Type = "https://httpstatuses.com/503"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred.",
                Type = "https://httpstatuses.com/500"
            }
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        var payload = JsonSerializer.Serialize(problem);
        await httpContext.Response.WriteAsync(payload, cancellationToken);
        return true;
    }
}
```

Use application-specific exception types and map only the categories your API deliberately exposes. For known exceptions, `Detail` is their specific message; for unrecognized exceptions, return the fixed safe detail rather than exposing `Exception.Message`. Repository code evidence is documented in [central exception-to-HTTP translation](../../features/validation-and-security/README.md#pattern-central-exception-to-http-translation).

## Standards for reuse in other .NET projects

1. Separate host composition from business orchestration.
2. Keep infrastructure decisions behind interfaces and configuration.
3. Use typed options and environment binding rather than direct config access in services.
4. Build the application around stable contracts, not specific SDK types.
5. Model idempotency and failure transitions at the boundary that owns them; configure dependency timeout and resilience policy at the dependency-client boundary.
6. Use middleware for cross-cutting concerns and cleanup the controller layer.
7. Use source-generated logging and structured telemetry as a first-class concern.
8. Keep validation and error mapping consistent with upstream/downstream contracts.
9. Expose health checks for runtime readiness and dependency reachability.
10. Test behavior at the layer that owns it: unit, host integration, and runtime E2E.

## Related docs

- [repository_architecture_rules.md](repository_architecture_rules.md)
- [feature_delivery_workflow.md](../feature_delivery_workflow.md)
- [../documentation/documentation_standards.md](../documentation/documentation_standards.md)
- [../documentation/engineering_documentation_standard.md](../documentation/engineering_documentation_standard.md)
