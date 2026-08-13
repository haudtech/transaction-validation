# Implementation Phases for TransactionValidation

This document defines ordered implementation phases for the Transaction Validation BFF.
Each phase mirrors the scaffold in `docs/implementation/implementation_scaffold.md` and describes the concrete code that must be generated in sequence.

## Principles

- Implement in sequence. Do not start a later phase until the prior phase is complete.
- Each phase must generate actual code/files, not just design notes.
- The implementation must follow the scaffold structure and options described in the scaffold doc.
Use the shared configuration project for wiring and keep the API project minimal.
Keep the Web API behavior aligned with the assignment: validation, partner verification, queue publishing, resiliency, security, and observability.
- The project standard is .NET 8 only.
- Use Central Package Management via `Directory.Packages.props`; avoid inline `Version` attributes in project `PackageReference` entries.

---

## Implementation Checklist

Follow this checklist to ensure each phase is implemented and documented before moving to the next. Mark child-items complete as you implement them.

### Phase 1 — Solution and Project Setup
- [ ] Create `TransactionValidation.sln`
- [ ] Create projects under `src/` and `tests/` (Api, Configuration, Core, Integration, Messaging, Mock, Tests)
- [ ] Ensure each project has a `.csproj` and correct target framework (`net8.0`)
- [ ] Add repository-level build standards: `global.json`, `Directory.Build.props`, `.editorconfig`
- [ ] Add central package management: `Directory.Packages.props`
- [ ] Add solution project references as described in scaffold
- [ ] Commit and verify `dotnet build` succeeds

### Phase 2 — Core Domain and Validation
- [ ] Add models: `PartnerTransactionRequest`, `TransactionEnvelope`, `ErrorResponse`
- [ ] Add interfaces: `IPartnerVerifier`, `IMessagePublisher`
- [ ] Implement manual validator and FluentValidation validator
- [ ] Add unit tests for validation
- [ ] Commit and run `dotnet test` for related tests

### Phase 3 — Shared Configuration and Middleware
- [ ] Create option classes: `ApiKeyOptions`, `PartnerVerificationOptions`, `RabbitMqOptions`
- [ ] Implement `ServiceCollectionExtensions.AddTransactionValidationCommonServices(IConfiguration)`
- [ ] Implement `ApiKeyMiddleware` and `GlobalExceptionHandlerMiddleware`
- [ ] Register OpenTelemetry and conditional App Insights exporter
- [ ] Wire FluentValidation auto-validation and validators registration
- [ ] Commit and smoke-test app startup with shared services
 - [ ] Define domain-specific exception types (e.g. `NotFoundException`, `BadRequestException`, `ConflictException`)
 - [ ] Implement an `IExceptionHandler` (or equivalent centralized handler) that maps exceptions to HTTP status codes and writes RFC 7807 `ProblemDetails`
 - [ ] Register the `IExceptionHandler` in the shared configuration and add unit tests for mappings

### Phase 4 — Mock Partner Verification API
- [ ] Implement `MockPartnerVerificationController` with `GET /api/v1/mock/partner-verification/verify/{partnerId}`
- [ ] Simulate timeout behavior (30%) and verified responses
- [ ] Add integration test or manual smoke test to call mock endpoint
- [ ] Commit mock project and verify it runs independently

### Phase 5 — Integration and Messaging Implementations
- [ ] Implement `PartnerVerifierClient` using `HttpClient` and Polly policies
- [ ] Implement `RabbitMqMessagePublisher` with durable queue and publisher confirms
- [ ] Add configuration binding for their options and register in DI
- [ ] Add unit tests/mocks for resiliency and publisher confirm failure
- [ ] Commit and run tests

### Phase 6 — API Project and Endpoint
- [ ] Implement `Program.cs` to use shared configuration and Serilog
- [ ] Implement `PartnerTransactionsController` POST endpoint
- [ ] Ensure request validation, partner verification, publishing and 202 Accepted response
- [ ] Add Swagger/OpenAPI for local discovery
- [ ] Commit and run end-to-end manual test locally

### Phase 7 — Observability and Azure Integration
- [ ] Ensure Serilog configuration and console sink work locally
- [ ] Verify OpenTelemetry tracing is emitted locally to console exporter
- [ ] Add Azure Monitor / App Insights conditional exporter and test (if connection string provided)
- [ ] Commit observability changes

### Phase 8 — Testing and Quality
- [ ] Add xUnit tests: validation, partner verifier, message publisher
- [ ] Separate test folders into `Unit/` and `Integration/` under `tests/TransactionValidation.Tests`
- [ ] Ensure unit test files mirror `src/` structure under `tests/TransactionValidation.Tests/Unit/`
- [ ] Mark integration tests with `[Trait("Category", "Integration")]`
- [ ] Achieve green tests for modified components
- [ ] Ensure CI policy: unit tests in `ci.yml` (`Category!=Integration`) and integration tests in `integration.yml` (`Category=Integration`)
- [ ] Commit tests and CI config

### Phase 9 — Docker and Local Infrastructure
- [ ] Add `Dockerfile` for Api and Mock projects
- [ ] Add `docker-compose.yml` that runs api, mock and rabbitmq
- [ ] Add `.env.example` with UPPER_SNAKE_CASE env names
- [ ] Verify `docker compose up --build` launches services
- [ ] Commit docker files and docs

### Phase 10 — Documentation and Final Review
- [ ] Update `implementation_scaffold.md` and `implementation_phases.md` to reflect final choices
- [ ] Add README with run steps and environment setup
- [ ] Do a final run-through and address any remaining TODOs
- [ ] Tag or release the completed scaffold

---

## Phase 1: Solution and Project Setup

### Goal
Create the solution and project structure required to host the implementation.

### Tasks
- Create `TransactionValidation.sln`.
- Create projects:
  - `src/TransactionValidation.Api` (Web API)
  - `src/TransactionValidation.Configuration` (shared startup/config)
  - `src/TransactionValidation.Core` (domain/validation/interfaces)
  - `src/TransactionValidation.Integration` (partner verification client)
  - `src/TransactionValidation.Messaging` (message publisher)
  - `src/TransactionValidation.Mock` (mock partner verification endpoint)
  - `tests/TransactionValidation.Tests` (unit tests)
- Add project references:
  - API references configuration/core/integration/messaging
  - Configuration references core
  - Integration and messaging reference core
  - Mock references core
  - Tests reference API/core/integration

### Deliverables
- Working `.sln` file
- Initial project `.csproj` files
- Correct project references and solution structure

---

## Phase 2: Core Domain and Validation

### Goal
Implement core models, interfaces, and validation contracts.

### Tasks
- Create `src/TransactionValidation.Core/Models/PartnerTransactionRequest.cs`
- Create `src/TransactionValidation.Core/Models/TransactionEnvelope.cs`
- Create `src/TransactionValidation.Core/Models/ErrorResponse.cs`
- Create `src/TransactionValidation.Core/Interfaces/IPartnerVerifier.cs`
- Create `src/TransactionValidation.Core/Interfaces/IMessagePublisher.cs`
- Create `src/TransactionValidation.Core/Validation/PartnerTransactionValidator.cs`
- Create `src/TransactionValidation.Core/Validation/PartnerTransactionRequestValidator.cs`
- Ensure `PartnerTransactionRequestValidator` is compatible with FluentValidation and message validation semantics

### Deliverables
- Core domain types for the partner transaction request and queue envelope
- Validation logic in both manual and FluentValidation form
- Interfaces that drive the service abstractions

---

## Phase 3: Shared Configuration and Middleware

### Goal
Create the shared configuration project that centralizes startup wiring, options, and middleware.

### Tasks
- Create option classes in `src/TransactionValidation.Configuration/Options`:
  - `ApiKeyOptions.cs`
  - `PartnerVerificationOptions.cs`
  - `RabbitMqOptions.cs`
- Create DI and middleware registration extensions in `src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs`
- Implement shared middleware in `src/TransactionValidation.Configuration/Middleware`:
  - `ApiKeyMiddleware.cs`
  - `GlobalExceptionHandlerMiddleware.cs`
- Register:
  - configuration binding
  - `PartnerVerifierClient` HTTP client
  - RabbitMQ message publisher
  - OpenTelemetry tracing
  - FluentValidation auto-validation
  - App Insights / Azure Monitor exporter if configured
- Ensure App Insights is conditional on `ApplicationInsights:ConnectionString`

### Deliverables
- Shared configuration project compiled and reusable
- Middleware wiring available via `UseTransactionValidationCommon()`

---

## Phase 4: Mock Partner Verification API

### Goal
Implement the dummy partner verification endpoint used by the integration client.

### Tasks
- Create `src/TransactionValidation.Mock/Controllers/MockPartnerVerificationController.cs`
- Implement the route `GET /api/v1/mock/partner-verification/verify/{partnerId}`
- Simulate 30% timeout behavior
- Return verified response 70% of the time
- Ensure the endpoint is valid in the mock project and can be run separate from the API

### Deliverables
- Mock verification controller that matches the assignment requirement
- Local mock API that can be started independently or together with the API

---

## Phase 5: Integration and Messaging Implementations

### Goal
Implement external integration and queue publisher components.

### Tasks
- Create `src/TransactionValidation.Integration/PartnerVerificationOptions.cs` if not already created in configuration
- Implement `src/TransactionValidation.Integration/PartnerVerifierClient.cs`
  - Use `HttpClient`
  - Apply Polly retry + timeout + circuit breaker policies
  - Call the mock verification endpoint
- Create `src/TransactionValidation.Messaging/RabbitMqOptions.cs` if not already created in configuration
- Implement `src/TransactionValidation.Messaging/RabbitMqMessagePublisher.cs`
  - Declare durable RabbitMQ queue
  - Publish persistent messages
  - Use publisher confirms
  - Throw if confirms fail

### Deliverables
- Resilient partner verification client
- RabbitMQ message publisher with confirm semantics

---

## Phase 6: API Project and Endpoint

### Goal
Build the API entrypoint and controller for the partner transaction workflow.

### Tasks
- Implement `src/TransactionValidation.Api/Program.cs`
  - Use Serilog startup
  - Use the shared configuration project
- Implement `src/TransactionValidation.Api/Controllers/PartnerTransactionsController.cs`
  - Accept `POST /api/v1/partner/transactions`
  - Validate request via FluentValidation / model validation
  - Verify partner via `IPartnerVerifier`
  - Publish queue message via `IMessagePublisher`
  - Return `202 Accepted` on success
- Ensure the controller returns structured error responses for validation and partner verification failures
- Add Swagger/OpenAPI support for local discovery

### Deliverables
- API controller and startup configured with shared services
- Minimal, well-structured API project implementation

---

## Phase 7: Observability and Azure Integration

### Goal
Add logging, telemetry, and Azure monitoring support.

### Tasks
- Ensure `TransactionValidation.Api` uses Serilog with console sink
- Confirm OpenTelemetry tracing is registered in configuration project
- Add App Insights / Azure Monitor exporter support
- Add `ApplicationInsights:ConnectionString` to `appsettings.json` as an optional value
- Keep local development working with console tracing only

### Deliverables
- Observability-enabled service with conditional Azure Monitor export
- Local developer experience that does not require Azure configuration

---

## Phase 8: Testing and Quality

### Goal
Implement test coverage for validation and resilience.

### Tasks
- Add xUnit tests in `tests/TransactionValidation.Tests`
- Create `ValidationTests.cs` for request validation scenarios
- Create `PartnerVerifierTests.cs` for service retry/resilience behavior
- Add additional tests for publisher confirms or message publisher behavior if needed
- Organize tests into `Unit/` and `Integration/` folders
- Ensure integration tests use `[Trait("Category", "Integration")]`

### Deliverables
- Unit test project with passing tests
- Early confidence in validation and external integration behavior

---

## Phase 9: Docker and Local Infrastructure

### Goal
Enable local development and demo deployment with Docker.

### Tasks
- Add `docker-compose.yml` at the repo root
- Add Dockerfiles for `TransactionValidation.Api` and `TransactionValidation.Mock`
- Configure RabbitMQ service in compose
- Ensure environment variables are set for API key, RabbitMQ, and mock base URL

### Deliverables
- Docker compose setup that runs API, mock, and RabbitMQ locally
- Clear instructions to start the system

---

## Phase 10: Documentation and Final Review

### Goal
Capture how the system works and how to run it.

### Tasks
- Ensure `docs/implementation/implementation_scaffold.md` remains the canonical scaffold
- Add a README or summary describing how to build, run, and test
- Confirm the ordering of phases and that each phase produced actual code
- Review the implemented files against the scaffold

### Deliverables
- Final documentation aligned with the phase-based implementation plan
- Confirmation that the actual code was generated in order with the required principle rules
