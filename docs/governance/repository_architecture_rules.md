# Repository Architecture Rules

## Purpose and Scope

This document records the repository-specific architecture and dependency rules that are enforced by the codebase, the configuration model, and the testing model. It is intentionally repository-specific and narrower than general engineering guidance.

The rules below describe responsibilities and boundaries that are currently verified by the code and tests in this solution.

## Core boundaries

### API host boundary

The `TransactionValidation.Api` project owns the HTTP host, middleware composition, health endpoints, routing, API-key enforcement, controller orchestration, and configuration registration. It does not own the implementation of the broker protocol or the external partner verification client.

### Core validation and orchestration boundary

The `TransactionValidation.Core` project owns the domain validation, data contracts, and orchestration semantics that are independent of the broker transport and hosting layer. It should remain stable under both local and Azure deployment modes.

### Configuration boundary

The `TransactionValidation.Configuration` project owns typed options and the registration extension methods that bind configuration values to business and infrastructure services. This is the repository's configuration control plane.

### Messaging boundary

The `TransactionValidation.Messaging` project owns the broker-neutral interfaces and the Concrete RabbitMQ and Azure Service Bus implementations. The application layer depends on abstractions, not broker-specific types.

### Test boundary

The `TransactionValidation.Tests` project owns the repository validation surface. It provides the unit, integration-host, and end-to-end checks that prove the runtime model and delivery expectations.

## Enforced rules

1. Broker logic must remain behind abstractions and configuration-driven registration.
2. The API host must expose `/healthz` and must allow health checks without partner credentials.
3. Local runtime behavior must be configurable through `.env` values and environment variables rather than by editing code for simple environment changes.
4. Infrastructure creation must remain in Bicep and the GitHub Actions infra workflow, not in the routine app deployment path.
5. Generated evidence under `TestResults/` is runtime output, not permanent design authority.
6. Feature documentation must describe current behavior, not historical implementation scaffolds or speculative future plans.
7. The root README and docs index remain the navigation authority; local implementation history and temporary artifacts do not form the permanent system model.

## Dependency direction

The repository should follow the same dependency direction across projects:

- API -> Configuration -> Core/Messaging
- Mock -> Configuration -> Messaging
- Tests -> API and service implementations
- Infrastructure -> Azure resource definitions and workflow outputs

The most important rule is that runtime behavior is assembled at the host boundary rather than by embedding infrastructure decisions inside the core domain logic.

## Runtime invariants

- Health checks remain accessible without a partner API key.
- Runtime broker choice is a configuration decision, not a code branching requirement in business logic.
- Idempotency state continues to be selected by the configuration model and environment-independent contract.
- Local Docker Compose is a development topology; Azure resources are a production-like deployment environment with its own security and identity model.

## Related documentation

- [documentation_standards.md](documentation_standards.md) — permanent authoring rules and documentation boundary policy
- [../features/runtime-and-api/README.md](../features/runtime-and-api/README.md)
- [../features/messaging/README.md](../features/messaging/README.md)
- [../features/distributed-state/README.md](../features/distributed-state/README.md)
- [../features/infrastructure-and-delivery/README.md](../features/infrastructure-and-delivery/README.md)
- [../azure_deployment/README.md](../azure_deployment/README.md)
