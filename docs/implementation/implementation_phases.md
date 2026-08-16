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

Use the dedicated checklist document for tracking progress and completion:

- [docs/implementation/implementation_checklist.md](implementation_checklist.md)

This phase guide stays focused on the implementation sequence, rationale, and expected deliverables for each stage.

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
- Create DI and startup registration extensions in `src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs`
- Implement shared middleware in `src/TransactionValidation.Configuration/Middleware`:
  - `ApiKeyMiddleware.cs`
  - `ApiExceptionHandler.cs`
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
  - Apply .NET 8 resilience handler policies (retry + timeout + circuit breaker) via `Microsoft.Extensions.Http.Resilience`
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
  - Enforce idempotency with in-memory TTL store (10-15 min window)
  - Use `Idempotency-Key` header when present, otherwise fallback to `partnerId|transactionReference`
  - Replay same `202 Accepted` response for same key+same payload and return conflict for same key+different payload
  - Verify partner via `IPartnerVerifier`
  - Publish queue message via `IMessagePublisher`
  - Return `202 Accepted` on success
- Ensure the controller returns structured error responses for validation and partner verification failures
- Add Swagger/OpenAPI support for local discovery

### Deliverables
- API controller and startup configured with shared services
- API-level idempotency semantics for duplicate replay and payload-mismatch protection
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
Implement test coverage for validation, resilience, and API startup integration behavior.

### Tasks
- Add xUnit tests in `tests/TransactionValidation.Tests`
- Create `ValidationTests.cs` for request validation scenarios
- Create `PartnerVerifierTests.cs` for service retry/resilience behavior (unit-test-first)
- Add additional tests for publisher confirms or message publisher behavior if needed
- Organize tests into `Unit/` and `Integration/` folders
- Ensure integration tests use `[Trait("Category", "Integration")]`
- Add integration-host tests for API startup using `WebApplicationFactory<Program>`
- Validate host-level behaviors through real middleware and exception pipeline (auth, idempotency, ProblemDetails exception mapping)
- Verify resilience wiring confidence in startup/DI through host-level tests while keeping policy timing semantics in unit tests

### Deliverables
- Unit test project with passing tests
- Integration-host API tests that execute startup and middleware pipeline in-memory
- Resilience strategy covered at two layers: unit policy behavior and host-level startup wiring confidence
- Early confidence in validation, external integration behavior, and runtime request pipeline

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
