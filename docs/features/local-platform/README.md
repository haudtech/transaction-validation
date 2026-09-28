# Local Platform

## Purpose and Scope

This document covers the implemented local runtime model for TransactionValidation. It explains how the API, Mock service, Redis, RabbitMQ, and environment variables are composed for local development and how the project switches between broker modes without changing the application contract.

It does not replace runtime feature docs or Azure deployment runbooks. It describes the repository's local operating model and the important environment boundaries that make that model reliable.

## Behavior Inventory

- Local Compose startup for API, Mock, Redis, and RabbitMQ
- Host and container port mapping
- Environment variable loading from `.env` and `.env.example`
- Broker selection between RabbitMQ and Azure Service Bus
- Local Redis-backed idempotency and health checks
- Local HTTP validation of API and Mock services
- Graceful local shutdown and environment reset

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Local runtime composition | Docker Compose | Single Compose stack runs the API, Mock, Redis, and RabbitMQ | `docker-compose.yml` | Compose and E2E task review |
| Local configuration | `.env.example` + env-file loading | Repository-level local settings without committing live secrets | `.env.example`, Compose files | Repo config review |
| Broker selection | ASP.NET configuration model | Broker type determined by config values and registration functions | `Program.cs`, configuration extensions | Runtime and messaging feature docs |
| Local distributed state | Redis | Redis-backed idempotency store and health checks | `IdempotencyStoreRegistration`, Docker Compose | Runtime and E2E tests |
| Local broker runtime | RabbitMQ | Independent queues, exchange/topic routing, and mock consumer subscriptions | Docker Compose and Mock app | E2E runtime matrix |
| Health verification | ASP.NET Core health checks | `/healthz` endpoint on both hosts | `Program.cs` and health-check registration | Runtime feature and validation docs |

## Definitions and Semantics

### Local runtime topology

The local Docker stack runs the following components together:

- API host on port `5000` by default
- Mock verification service on port `5002`
- RabbitMQ on `5672` and management on `15672`
- Redis on `6379`

The API is configured to use Redis for distributed idempotency and RabbitMQ as the default broker in the Compose environment. The Mock service participates in the broker topology and exposes its own HTTP surface for API validation and partner simulation.

### Environment selection model

The repository uses a `.env` file for local overrides and keeps `.env.example` as the template. Values are loaded by Docker Compose and by the configuration pipeline. This allows the same code to switch between:

- local RabbitMQ + Redis
- local Azure Service Bus mode when the namespace values are supplied
- Azure-hosted apps using managed identity and environment-provided config

The selection is explicit and configuration-driven, not implicit or hardcoded in the app logic.

### Local health and readiness

Both application hosts map `/healthz` and allow platform probes without the partner API key. This is important for Compose health verification and for Azure Container Apps readiness checks.

## Configuration Contract

The local development contract is intentionally simple and explicit:

- `API_HOST_PORT` selects the published port for the API container
- `PARTNERVERIFICATION__BASEURL` points the API at the local Mock service
- `REDIS__CONNECTIONSTRING` configures the idempotency store
- `MESSAGING__BROKERTYPE` selects the runtime broker mode
- RabbitMQ and Service Bus variables are environment-specific and may be left empty when a path is not in use
- `SECURITY__APIKEY` is required in the app layer when API-key enforcement is enabled

The repository keeps these values in `.env.example` and expects local overrides to remain out of source control.

## Source Ownership

Primary owners:

- `docker-compose.yml` — local service topology, port mapping, broker setup, and Redis health checks
- `.env.example` — local configuration template for the repository
- `src/TransactionValidation.Api/Program.cs` — host composition, health checks, and broker registration
- `src/TransactionValidation.Mock/` — local verification endpoint and broker consumers
- `.vscode/tasks.json` — local quality and E2E task entry points

Supporting owners:

- `../runtime-and-api/README.md` — host and API contract boundaries
- `../messaging/README.md` — broker semantics and local vs Azure differences
- `../testing-and-quality/README.md` — local E2E and coverage model

## Verification Surface

The following sources verify the local platform behavior:

- Docker Compose service definition and startup ordering
- `.env.example` and local environment semantics
- `Program.cs` health checks and configuration registration
- E2E task validation for RabbitMQ routing and replay behavior
- local quality tasks for build/test/coverage and Compose startup/shutdown logic

## Implementation Pattern Rules

### Pattern: keep the local stack broker-neutral at the app boundary

- **Applies to** - local and Azure runtime configuration.
- **Intent** - allow the same application logic to run with either RabbitMQ or Azure Service Bus.
- **Rule** - the repository MUST select the broker through configuration and registration boundaries. The runtime contract SHOULD remain the same across modes even when the underlying transport changes.
- **Approved code shape**:

```csharp
builder.Services.AddConfiguredBroker(
    builder.Configuration,
    AddRabbitMqMessagingServices,
    AddAzureServiceBusMessagingServices);
```

- **Avoid** - broker-specific code paths deep inside the core application logic or local-only runtime assumptions in the business contract.
- **Implementation references** - `Program.cs`, message registration extension methods
- **Verification** - runtime and messaging feature tests

### Pattern: use the `.env` file for local overrides only

- **Applies to** - local runtime configuration.
- **Intent** - keep the default local flow reproducible while still allowing safe developer overrides.
- **Rule** - local environment values MUST be represented in `.env.example` and SHOULD be kept out of repository-scoped secrets. The Compose stack SHOULD allow environment overrides without changing source-controlled defaults.
- **Approved code shape**:

```yaml
env_file:
  - .env
environment:
  ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Development}
```

- **Avoid** - committing live keys or local-only secrets into the repository, or using fixed hostnames that assume one machine layout for all developers.
- **Implementation references** - `docker-compose.yml`, `.env.example`
- **Verification** - config review and local startup validation

### Pattern: prefer graceful shutdown and health verification over forceful cleanup

- **Applies to** - local development lifecycle.
- **Intent** - keep container and service shutdown predictable and safe.
- **Rule** - local operations SHOULD use `docker compose down` or `Ctrl+C` before forceful termination. Forceful termination is an exception rather than the default recovery path.
- **Approved code shape**:

```bash
docker compose down
docker compose down -v
```

- **Avoid** - routine `kill -9` usage in standard maintenance, or deleting volumes without a clear need for data reset.
- **Implementation references** - local development docs and compose lifecycle conventions
- **Verification** - local runbook review and operational guidance

## Operational Boundaries

- The local runtime is a deliberate development and validation environment, not the production architecture.
- The repository chooses RabbitMQ as the default local broker but allows Azure Service Bus to be enabled when the environment is configured for it.
- Local health checks and app startup depend on the presence of the Compose stack and valid environment file values.
- Local port values are expected to be consistent with the current Compose configuration and should be kept in sync with the operational docs and runtime checks.
