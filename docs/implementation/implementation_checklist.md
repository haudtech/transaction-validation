# Implementation Checklist

This document is the maintainable checklist for the TransactionValidation implementation phases. It is intentionally separate from the narrative phase guide in [implementation_phases.md](implementation_phases.md) so it can be updated quickly and referenced directly by contributors.

This checklist reflects the current repository state and marks only the work that is already implemented and verified in the codebase.

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
- [ ] Create option classes: `ApiKeyOptions`, `PartnerVerificationOptions`, `RabbitMqOptions`
- [ ] Implement `ServiceCollectionExtensions.AddTransactionValidationCommonServices(IConfiguration)`
- [ ] Implement `ApiKeyMiddleware` and `GlobalExceptionHandlerMiddleware`
- [ ] Register OpenTelemetry and conditional App Insights exporter
- [ ] Wire FluentValidation auto-validation and validators registration
- [ ] Commit and smoke-test app startup with shared services
- [ ] Define domain-specific exception types (e.g. `NotFoundException`, `BadRequestException`, `ConflictException`)
- [ ] Implement an `IExceptionHandler` (or equivalent centralized handler) that maps exceptions to HTTP status codes and writes RFC 7807 `ProblemDetails`
- [ ] Register the `IExceptionHandler` in the shared configuration and add unit tests for mappings

## Phase 4 — Mock Partner Verification API
- [x] Implement `MockPartnerVerificationController` with `GET /api/v1/mock/partner-verification/verify/{partnerId}`
- [ ] Simulate timeout behavior (30%) and verified responses
- [ ] Add integration test or manual smoke test to call mock endpoint
- [ ] Commit mock project and verify it runs independently

## Phase 5 — Integration and Messaging Implementations
- [ ] Implement `PartnerVerifierClient` using `HttpClient` and Polly policies
- [ ] Implement `RabbitMqMessagePublisher` with durable queue and publisher confirms
- [ ] Add configuration binding for their options and register in DI
- [ ] Add unit tests/mocks for resiliency and publisher confirm failure
- [ ] Commit and run tests

## Phase 6 — API Project and Endpoint
- [ ] Implement `Program.cs` to use shared configuration and Serilog
- [ ] Implement `PartnerTransactionsController` POST endpoint
- [ ] Ensure request validation, partner verification, publishing and 202 Accepted response
- [ ] Add Swagger/OpenAPI for local discovery
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
