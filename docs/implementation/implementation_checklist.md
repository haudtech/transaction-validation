# Implementation Checklist

This document is the maintainable checklist for the TransactionValidation implementation phases. It is intentionally separate from the narrative phase guide in [implementation_phases.md](implementation_phases.md) so it can be updated quickly and referenced directly by contributors.

This checklist reflects the current repository state and marks only the work that is already implemented and verified in the codebase.

> Prerequisite gate: before a phase is considered ready, the required tooling, dependencies, and environment items from [Prerequisites/README.md](Prerequisites/README.md) must be prepared first. The checklist below references the prerequisite groups that must be in place.

---

## Prerequisite links by phase

- Phase 1 — .NET SDK, `dotnet` CLI, IDE/editor readiness: [Prerequisites/README.md](Prerequisites/README.md#1-net-sdk), [Prerequisites/README.md](Prerequisites/README.md#2-dotnet-cli), [Prerequisites/README.md](Prerequisites/README.md#3-ide--editor)
- Phase 2 — .NET SDK and toolchain readiness: [Prerequisites/README.md](Prerequisites/README.md#1-net-sdk), [Prerequisites/README.md](Prerequisites/README.md#2-dotnet-cli)
- Phase 3 — NuGet packages, configuration, optional Azure monitoring: [Prerequisites/README.md](Prerequisites/README.md#5-nuget-package-dependencies), [Prerequisites/README.md](Prerequisites/README.md#7-environment-and-configuration), [Prerequisites/README.md](Prerequisites/README.md#6-optional-infrastructure)
- Phase 4 — .NET SDK and local runtime readiness: [Prerequisites/README.md](Prerequisites/README.md#1-net-sdk), [Prerequisites/README.md](Prerequisites/README.md#2-dotnet-cli), [Prerequisites/README.md](Prerequisites/README.md#3-ide--editor)
- Phase 5 — RabbitMQ, NuGet dependencies, environment configuration: [Prerequisites/README.md](Prerequisites/README.md#4-rabbitmq), [Prerequisites/README.md](Prerequisites/README.md#5-nuget-package-dependencies), [Prerequisites/README.md](Prerequisites/README.md#7-environment-and-configuration)
- Phase 6 — .NET SDK, appsettings configuration, local runtime: [Prerequisites/README.md](Prerequisites/README.md#1-net-sdk), [Prerequisites/README.md](Prerequisites/README.md#7-environment-and-configuration), [Prerequisites/README.md](Prerequisites/README.md#8-recommended-cli-commands)
- Phase 7 — Serilog/OpenTelemetry packages, optional Azure connection string: [Prerequisites/README.md](Prerequisites/README.md#5-nuget-package-dependencies), [Prerequisites/README.md](Prerequisites/README.md#6-optional-infrastructure), [Prerequisites/README.md](Prerequisites/README.md#7-environment-and-configuration)
- Phase 8 — .NET SDK + test dependencies: [Prerequisites/README.md](Prerequisites/README.md#5-nuget-package-dependencies), [Prerequisites/README.md](Prerequisites/README.md#8-recommended-cli-commands)
- Phase 9 — Docker, Docker Compose, RabbitMQ infrastructure: [Prerequisites/README.md](Prerequisites/README.md#4-rabbitmq), [Prerequisites/README.md](Prerequisites/README.md#6-optional-infrastructure)
- Phase 10 — all previous prerequisites and final repo readiness: [Prerequisites/README.md](Prerequisites/README.md)

---

## Phase 1 — Solution and Project Setup
- [x] Create `TransactionValidation.sln`
- [x] Create projects under `src/` and `tests/` (Api, Configuration, Core, Integration, Messaging, Mock, Tests)
- [x] Ensure each project has a `.csproj` and correct target framework (`net8.0`)
- [x] Add repository-level build standards: `global.json`, `Directory.Build.props`, `.editorconfig`
- [x] Add central package management: `Directory.Packages.props`
- [x] Add solution project references as described in scaffold
- [x] Commit and verify `dotnet build` succeeds

## Phase 2 — Core Domain and Validation
- [x] Add models: `PartnerTransactionRequest`, `TransactionEnvelope`, `ErrorResponse`
- [x] Add interfaces: `IPartnerVerifier`, `IMessagePublisher`
- [x] Implement manual validator and FluentValidation validator
- [x] Add unit tests for validation
- [x] Commit and run `dotnet test` for related tests

## Phase 3 — Shared Configuration and Middleware
- [x] Phase 3 prerequisite gate is satisfied: NuGet packages are present, the API appsettings are in place, and the credential values are separated into the local environment file.
- [x] Create option classes: `ApiKeyOptions`, `PartnerVerificationOptions`, `RabbitMqOptions`, `OpenTelemetryOptions`, and `SerilogOptions`
- [x] Implement `ServiceCollectionExtensions.AddTransactionValidationCommonServices(IConfiguration)`
- [x] Implement `ApiKeyMiddleware`
- [x] Register OpenTelemetry and conditional App Insights exporter
- [x] Wire FluentValidation auto-validation and validators registration
- [ ] Commit and smoke-test app startup with shared services (implementation is in place; final shell-level validation is still blocked by the VS Code named-pipe sandbox issue)
- [x] Define domain-specific exception types (e.g. `NotFoundException`, `BadRequestException`, `ConflictException`)
- [x] Implement an `IExceptionHandler` that maps exceptions to HTTP status codes and writes RFC 7807 `ProblemDetails`
- [x] Register the `IExceptionHandler` in the shared configuration and add unit tests for mappings

## Phase 4 — Mock Partner Verification API
- [x] Implement `MockPartnerVerificationController` with `GET /api/v1/mock/partner-verification/verify/{partnerId}`
- [x] Simulate timeout behavior (30%) and verified responses
- [x] Add integration test or manual smoke test to call mock endpoint
- [ ] Commit mock project and verify it runs independently

## Phase 5 — Integration and Messaging Implementations
- [x] Implement `PartnerVerifierClient` using `HttpClient` and .NET 8 resilience policies (`Microsoft.Extensions.Http.Resilience`)
- [x] Implement `RabbitMqMessagePublisher` with durable queue and publisher confirms
- [x] Add configuration binding for their options and register in DI
- [x] Add unit tests/mocks for resiliency and publisher confirm failure
- [ ] Commit and run tests (tests are verified locally; commit step remains pending)

## Phase 6 — API Project and Endpoint
- [x] Implement `Program.cs` to use shared configuration and Serilog
- [x] Implement `PartnerTransactionsController` POST endpoint
- [x] Ensure request validation, partner verification, publishing and 202 Accepted response
- [x] Enforce idempotency in `CreateAsync` with in-memory TTL store and duplicate protection
- [x] Support `Idempotency-Key` header with fallback to `partnerId|transactionReference`
- [x] Reject key reuse with different payload using request fingerprint semantics
- [x] Add Swagger/OpenAPI for local discovery
- [ ] Commit and run end-to-end manual test locally

## Phase 7 — Observability and Azure Integration
- [ ] Ensure Serilog configuration and console sink work locally
- [ ] Verify OpenTelemetry tracing is emitted locally to console exporter
- [ ] Add Azure Monitor / App Insights conditional exporter and test (if connection string provided)
- [ ] Commit observability changes

## Phase 8 — Testing and Quality
- [x] Add xUnit tests: validation, partner verifier, message publisher
- [x] Separate test folders into `Unit/` and `Integration/` under `tests/TransactionValidation.Tests`
- [x] Ensure unit test files mirror `src/` structure under `tests/TransactionValidation.Tests/Unit/`
- [x] Mark integration tests with `[Trait("Category", "Integration")]`
- [x] Achieve green tests for modified components
- [x] Ensure CI policy: unit tests in `ci.yml` (`Category!=Integration`) and integration tests in `integration.yml` (`Category=Integration`)
- [x] Commit tests and CI config

## Phase 9 — Docker and Local Infrastructure
- [ ] Add `Dockerfile` for Api and Mock projects
- [ ] Add `docker-compose.yml` that runs api, mock and rabbitmq
- [ ] Add `.env.example` with UPPER_SNAKE_CASE env names
- [ ] Verify `docker compose up --build` launches services
- [ ] Commit docker files and docs

## Phase 10 — Documentation and Final Review
- [x] Update `implementation_scaffold.md` and `implementation_phases.md` to reflect final choices
- [x] Add README with run steps and environment setup
- [ ] Do a final run-through and address any remaining TODOs
- [ ] Tag or release the completed scaffold
