# TransactionValidation - Partner Integration BFF

TransactionValidation is a Backend-for-Frontend platform that receives transaction submissions from external partners, verifies that each request can be accepted, and routes accepted transactions to independent downstream consumers.

## Platform Purpose

The platform provides a controlled boundary between partner-facing HTTP traffic, an external partner-verification dependency, and asynchronous internal processing. It keeps transport and infrastructure choices behind stable application contracts so the transaction flow remains consistent across local and Azure environments.

## Request Orchestration

For each transaction submission, the platform:

1. **Authenticate and validate:** Verifies the calling partner and validates the request contract.
2. **Protect against duplicates:** Applies idempotency so safe retries replay the accepted result while conflicting payload reuse is rejected.
3. **Verify the partner:** Calls the external verification service through bounded resilience policies.
4. **Create and publish:** Builds a correlated transaction envelope and publishes it through the active message broker.
5. **Confirm acceptance:** Returns an accepted response only after the broker success boundary is reached.
6. **Route independently:** Delivers copies to primary and audit consumers according to broker routing rules.

If processing fails before acceptance, the platform returns a predictable API outcome and releases the idempotency claim so a valid retry can proceed.

## Testing and Quality

Quality is established through complementary validation levels:

| Quality layer | Description |
|---|---|
| Unit tests | Cover domain validation, idempotency stores, resilience behavior, exception mapping, message publication, and structured logging contracts. |
| Integration tests | Execute the ASP.NET Core host to verify middleware, dependency registration, authentication, idempotency, and API response behavior. |
| End-to-end tests | Exercise containerized services, real network boundaries, broker routing, independent consumers, selective delivery, and redelivery. |
| Automated gates | Verify formatting, compilation, unit and integration behavior, and coverage before changes are accepted. |
| Coverage policy | Enforces an 80% target for the filtered business and application logic included in the repository coverage policy. |

End-to-end validation remains separate from code coverage because it measures deployed runtime and infrastructure confidence rather than in-process line coverage.

## Observability and Operations

The platform emits correlated application and dependency signals for troubleshooting failures and measuring request-path performance.

| Signal | Implemented behavior |
|---|---|
| Structured logs | Source-generated events use stable event IDs and typed properties for transaction processing, partner verification, idempotency, publishing, broker topology, and consumer outcomes. |
| Correlation | A validated or generated correlation ID is added to the request logging scope and trace, returned with accepted responses, and propagated through transaction envelopes and broker metadata. |
| Traces | OpenTelemetry instruments incoming ASP.NET Core requests and outbound HTTP dependencies for the API and Mock services. |
| Metrics | OpenTelemetry records ASP.NET Core and outbound HTTP measurements, with console export available for local diagnostics. |
| Performance timing | Structured events record elapsed time for partner verification, transaction processing, and message publication. |
| Health checks | The API health endpoint reports publisher registration and Redis reachability when distributed idempotency is configured; the Mock service exposes a basic host health endpoint. |
| Azure telemetry | Infrastructure provisions Log Analytics and workspace-based Application Insights; application telemetry is exported through Azure Monitor when its connection string is supplied. |

The current implementation does not claim application dashboards, alerting rules, service-level objectives, or broker-consumer trace spans.

## Operating Modes

Local execution uses containerized application services and local infrastructure for development and end-to-end validation. Azure execution uses managed messaging, distributed idempotency, container hosting, centralized observability, managed identities, and protected delivery workflows.

Only one messaging implementation is active in a deployment, while the API contract, transaction envelope, idempotency semantics, and consumer responsibilities remain consistent.

## Technology Stack

| Domain | Technologies and techniques |
|---|---|
| Runtime and API | .NET 8, ASP.NET Core Web API, Swagger/OpenAPI |
| Validation and security | FluentValidation, API-key authentication, RFC 7807 ProblemDetails |
| Reliability | HTTP resilience pipelines, retry, timeout, circuit breaker, idempotency |
| Messaging | RabbitMQ, Azure Service Bus, topic-based fan-out, independent consumers |
| Distributed state | Redis, Azure Cache for Redis |
| Observability | Serilog, source-generated logging, OpenTelemetry, Azure Monitor, Application Insights, Log Analytics |
| Testing and quality | xUnit, Moq, FluentAssertions, WebApplicationFactory, Coverlet, ReportGenerator, Codecov |
| Local platform | Docker, Docker Compose |
| Azure platform | Container Apps, Key Vault, managed identities, private networking |
| Infrastructure and delivery | Bicep, GitHub Actions, OpenID Connect |

## Documentation

Use the [documentation index](docs/README.md) for architecture, API behavior, reliability, messaging, observability, testing, development, CI/CD, and Azure operations.
