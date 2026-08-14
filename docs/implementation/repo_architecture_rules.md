# TransactionValidation Repository Architecture Rules

**Document Version:** 2.0  
**Last Updated:** August 13, 2026  
**Status:** Active and tailored to this repository  
**Audience:** Contributors and reviewers  

---

## Purpose

This document defines the architecture rules that are relevant to this repository and intentionally removes rules that are unrelated to the current solution design.

This project is a .NET 8 Backend-for-Frontend (BFF) for partner transaction verification and queue publishing. It is intentionally lean and centered on project boundaries, interface-driven contracts, validation, resilience, security, and observability.

This repository does not implement EF Core persistence, repository abstractions, DbContext-based data access, or a large multi-layer service graph. Therefore, rules tied to those patterns are intentionally excluded or simplified here.

Related repo guidance:
- [docs/implementation/implementation_phases.md](implementation_phases.md)
- [docs/implementation/implementation_scaffold.md](implementation_scaffold.md)
- [docs/implementation/shared_engineering_principles.md](shared_engineering_principles.md)
- [README.md](../../README.md)

---

## 1. Project boundaries and dependency direction

### Rule 1.1: Respect the solution structure

The repository is intentionally split into purpose-specific projects:

- `TransactionValidation.Api` — inbound HTTP boundary and composition root
- `TransactionValidation.Configuration` — shared startup and DI wiring
- `TransactionValidation.Core` — contracts, models, validators, and domain concepts
- `TransactionValidation.Integration` — external system adapters
- `TransactionValidation.Messaging` — broker adapters
- `TransactionValidation.Mock` — mock external verification endpoint for local development
- `TransactionValidation.Tests` — xUnit test project

### Rule 1.2: Dependency direction is one-way

Dependencies MUST flow in this direction:

- API depends on Configuration, Core, Integration, Messaging
- Configuration depends on Core
- Integration and Messaging depend on Core
- Mock depends on Core
- Tests depend on the projects they validate

Circular dependencies, reverse dependencies from Core to Api, and cross-project leakage are not allowed.

### Rule 1.3: Keep the API project thin

`TransactionValidation.Api` is the HTTP boundary. It MUST handle:

- request binding
- middleware registration
- API key validation
- validation result mapping
- orchestration of dependency calls
- response creation

The API project MUST NOT contain business logic unrelated to orchestration or HTTP concerns.

### Rule 1.4: Keep Core contract-first

`TransactionValidation.Core` is the shared contract layer. It contains:

- DTOs and request models
- transport envelopes
- validation rules
- interfaces such as `IPartnerVerifier` and `IMessagePublisher`
- shared error models

Core SHOULD define the system vocabulary and avoid infrastructure or transport dependencies.

---

## 2. Shared principles adapted from common engineering practice

### Rule 2.1: One responsibility per project and contract

Each project and interface MUST represent a single concern:

- validation belongs in Core
- HTTP and composition belong in API
- partner verification belongs in Integration
- queue publishing belongs in Messaging
- mock behavior belongs in Mock

### Rule 2.2: Prefer explicit interfaces over hidden implementation details

Every dependency crossing a boundary MUST be exposed through an interface in Core.

Examples:

- `IPartnerVerifier`
- `IMessagePublisher`

This preserves testability and keeps the solution aligned with the contract-first design.

### Rule 2.3: Use configuration objects instead of magic values

Do not hard-code:

- partner URLs
- queue names
- retry counts
- timeout values
- API keys

All values should be loaded through configuration and environment overrides using the configuration project.

### Rule 2.4: Do not leak infrastructure concerns into Core

Core models and interfaces must not know about:

- ASP.NET Core result types
- RabbitMQ classes
- HttpClient wrappers
- logging abstractions
- Azure-specific types

Those implementation concerns belong in concrete infrastructure projects, not in Core.

---

## 3. Validation rules

### Rule 3.1: Validate at the entry boundary

Request validation MUST run before partner verification and queue publishing. The key rules are:

- `partnerId` is required
- `transactionReference` is required
- `amount > 0`
- `currency` is supported and well-formed
- `timestamp` is present and valid

This repo uses FluentValidation and shared validation helpers in Core.

### Rule 3.2: Validation logic belongs in the Core project

Validation rules belong in the core project and must be reusable, not duplicated across controllers and infrastructure classes.

### Rule 3.3: Error responses must be structured and consistent

The project SHOULD use consistent error objects such as `ErrorResponse` and `FieldError` rather than ad hoc strings.

Validation failures should be clear, machine-readable, and consistent across the API boundary.

### Rule 3.4: Infrastructure code is not responsible for business validation

Messaging and integration code SHOULD not own domain validation logic. Their purpose is transport and execution, not business correctness.

---

## 4. Security and request handling rules

### Rule 4.1: API key validation belongs in middleware

Authentication and request gating SHOULD happen at the middleware layer before business processing begins.

This is a BFF boundary concern and should not be mixed into validation classes or message publishers.

### Rule 4.2: Fail closed on security decisions

If the API key is missing, invalid, or misconfigured, the request MUST fail immediately with a security response.

### Rule 4.3: Never hard-code secrets

Keys, connection strings, and partner credentials MUST come from configuration or environment variables, not source code.

---

## 5. Resilience and external dependency rules

### Rule 5.1: Treat external systems as unsafe by default

Partner verification is an external dependency and transient failures are expected.

### Rule 5.2: Use bounded resilience policies

For partner verification calls, use timeout, retry, and circuit-breaker patterns when appropriate. `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler(...)` is the expected approach in this repo.

The design MUST ensure:

- timeouts are explicit
- retries are bounded
- transient failures are retried without masking a real outage
- the API fails predictably when the dependency is unavailable

### Rule 5.3: Message publication must be asynchronous and confirmable

For RabbitMQ publishing, prefer durable messages and publisher confirms. This aligns with the repo’s queue-based design.

The publisher MUST not claim success before a confirmed send or a clearly defined success condition.

### Rule 5.4: Keep external protocol details out of Core

HTTP client code, queue code, and retry logic belong in Integration and Messaging projects, not in Core.

---

## 6. Idempotency and request safety rules

### Rule 6.1: Duplicate requests are expected and should be handled deliberately

Because the API sits between clients and downstream systems, retries and duplicate submissions can happen.

The system SHOULD protect against duplicate processing using a stable key such as:

- `partnerId + transactionReference`

### Rule 6.2: Idempotency is a workflow concern, not a transport detail

Duplicate handling should be designed in the transaction workflow and documented clearly in the API behavior, rather than buried in ad hoc controller logic.

---

## 7. Configuration and environment rules

### Rule 7.1: Use a single configuration flow

The repo MUST use a clear and predictable precedence model for configuration sources.

Typical order:

1. appsettings.json
2. appsettings.{Environment}.json
3. Docker-specific configuration
4. environment variables
5. command-line arguments

### Rule 7.2: Keep startup composition in Configuration

Service registration and startup wiring MUST be centralized in the Configuration project rather than spread across the API project.

### Rule 7.3: Workspace consistency is part of project health

The editor and the solution graph must be aligned. The tracked default solution in `.vscode/settings.json` is part of the project’s developer workflow and should not be treated as optional local noise.

---

## 8. Observability and developer experience rules

### Rule 8.1: Logging must be structured and contextual

This repo expects structured logging and telemetry support using Serilog and OpenTelemetry patterns.

Logs should include key workflow context such as:

- partnerId
- transactionReference
- correlationId
- verification outcome
- publish outcome

### Rule 8.2: Observability is part of reliability

When a request fails, the system should provide actionable signal for:

- verification failures
- queue publishing failures
- validation failures
- security failures

### Rule 8.3: Local debugging and IntelliSense quality are SDLC requirements

A reliable local debug environment and stable IntelliSense are part of the development lifecycle. Workspace and solution stability should be treated as project health, not as personal editor preferences.

---

## 9. Testing rules

### Rule 9.1: Test public behavior, not implementation details

Unit tests should validate behavior at the contract and validation boundaries. They should not assert on fragile mock-only behavior where a real behavior can be verified.

### Rule 9.2: Keep tests aligned with project structure

The repository uses a unit and integration split under `tests/TransactionValidation.Tests` and keeps the test structure aligned with the source project layout.

### Rule 9.3: Focus on relevant behavior

Relevant test areas include:

- validation logic
- resilience and timeout behavior
- mock partner verification
- queue publish success and failure
- API boundary behavior where implemented

### Rule 9.4: Separate unit and integration concerns

- Unit tests: no external dependencies and no real queue or service setup
- Integration tests: real or emulated external dependencies and environment-specific flows

---

## 10. Explicitly removed or intentionally out-of-scope rules

The following patterns are intentionally not adopted for this repository because they do not match the actual solution design:

- EF Core repository / unit-of-work patterns
- DbContext-first domain layering
- large multi-service business-layer hierarchy
- heavy persistence-centric architecture
- vehicle-booking or appointment-scheduling examples unrelated to this BFF
- repository cache patterns for transactional data without a clear need

This keeps the architecture leaner and better aligned with the assignment.

---

## 11. Review checklist for new work

Before merging any change, confirm the following:

- [ ] The project boundary is respected
- [ ] Core contracts remain independent of infrastructure concerns
- [ ] Validation rules remain centralized and reusable
- [ ] External dependencies are isolated in Integration or Messaging
- [ ] Configuration remains environment-aware and non-hardcoded
- [ ] Security checks occur at the correct boundary
- [ ] Observability context is available for failures
- [ ] Tests cover the behavior affected by the change
- [ ] Documentation and phase checklist remain aligned with the actual implementation

---

## Final principle

The architecture for this repository is intentionally simple, explicit, and contract-driven. It favors clarity over abstraction, boundary correctness over convenience, and reliability over unnecessary framework complexity.

This is the standard this repo should follow.
