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

## Architecture terminology and service boundaries

Describe this solution as **service-oriented, with separately hosted components and explicit project boundaries**. Separate projects or executable hosts alone do not prove that the system is a microservice architecture.

Use the term **microservice** for a component only when the architecture evidence supports the relevant properties, including independent deployment, a clearly bounded responsibility and ownership, and an appropriately owned data/state boundary. If those properties are not established, prefer precise terms such as project, executable host, service, adapter, or component. Reassess the terminology when deployment, team ownership, or data boundaries materially change.

This is a repository-specific documentation and design constraint, not a universal definition that every architecture must implement identically. Architecture documents should describe actual deployment and ownership boundaries rather than infer them from solution/project layout.

## Canonical coding patterns applied by this architecture

Reusable implementation rules and C# examples are owned by [Coding Standards](coding_standards.md). This section records only how those rules apply to TransactionValidation; it does not restate their requirements.

| Canonical rule | Application in this repository | Concrete evidence |
|---|---|---|
| [CS-HOST-01 — Keep executable hosts thin and explicit](coding_standards.md#cs-host-01-keep-executable-hosts-thin-and-explicit) | API and Mock hosts compose shared services and runtime-specific registrations; they are separately hosted components, not thereby independent microservices. | [Runtime and API host composition](../../features/runtime-and-api/README.md#pattern-thin-executable-composition-root). |
| [CS-EXT-01 — Keep external protocol and SDK details inside adapters](coding_standards.md#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters) | Core-facing contracts remain broker-neutral; `Integration` translates partner HTTP outcomes and `Messaging` owns RabbitMQ/Service Bus adapters. | [Messaging publication contract](../../features/messaging/README.md#pattern-publish-through-a-transport-neutral-contract); [partner outcome translation](../../features/reliability/README.md#pattern-translate-terminal-dependency-outcomes-at-the-adapter). |
| [CS-CFG-01 — Bind and validate configuration at the composition boundary](coding_standards.md#cs-cfg-01-bind-and-validate-configuration-at-the-composition-boundary) | Typed options and broker/state implementation selection are bound and composed at host startup. | [Runtime selection](../../features/runtime-and-api/README.md#pattern-single-runtime-implementation-selection); [repository configuration rules](../configuration/repository_configuration_reference.md). |
| [CS-FLOW-01 — Validate before side effects and make state transitions explicit](coding_standards.md#cs-flow-01-validate-before-side-effects-and-make-state-transitions-explicit) | The API validates requests before acquiring idempotency state and releases acquired state after downstream failure. | [Validation-before-side-effects](../../features/validation-and-security/README.md#pattern-validate-before-side-effects); [distributed state contract](../../features/distributed-state/README.md#pattern-store-behind-one-workflow-contract). |
| [CS-OBS-01 — Propagate context while minimizing sensitive telemetry](coding_standards.md#cs-obs-01-propagate-context-while-minimizing-sensitive-telemetry) | Correlation and message identity cross the API and broker boundaries; generated logging catalogs are used for operational events. | [Observability patterns](../../features/observability/README.md#governance-rule-traceability); [logging standards](../../operations/diagnostics/logging_standards.md). |
| [CS-TEST-01 — Test observable behavior at the owning boundary](coding_standards.md#cs-test-01-test-observable-behavior-at-the-owning-boundary) | Unit, real-host integration, and broker-backed E2E tests verify different contracts. | [Testing and quality patterns](../../features/testing-and-quality/README.md#governance-rule-traceability). |
| [CS-TEST-03 — Exercise host integration boundaries without replacing the host](coding_standards.md#cs-test-03-exercise-host-integration-boundaries-without-replacing-the-host) | API integration tests retain the real `WebApplicationFactory<Program>` pipeline and replace only selected external dependencies. | [Real-host integration pattern](../../features/testing-and-quality/README.md#pattern-exercise-the-real-host-for-boundary-integration). |
| [CS-TEST-04 — Reserve E2E tests for critical runtime contracts](coding_standards.md#cs-test-04-reserve-e2e-tests-for-critical-runtime-contracts) | Repository topology E2E uses Docker Compose and real HTTP/broker paths; no GitHub E2E workflow currently exists. | [E2E runtime pattern and matrix](../../features/testing-and-quality/README.md#pattern-reserve-e2e-for-runtime-boundaries); [E2E runtime matrix](../../operations/testing/test/e2e_runtime_matrix.md). |

The [Observability feature](../../features/observability/README.md) owns the implementation-specific source-generated logging and health-check details. The [feature delivery workflow](../feature_delivery_workflow.md) governs how applicable rule IDs and evidence are recorded for changes.

## Enforced rules

1. Concrete project responsibilities and dependency direction MUST remain consistent with the boundaries in this document.
2. Runtime broker selection MUST remain configuration-driven; application workflows MUST NOT branch on RabbitMQ or Azure Service Bus implementation types.
3. The API host MUST expose `/healthz`, and API-key middleware MUST allow the health endpoint without partner credentials.
4. Local runtime behavior MUST remain configurable through local environment inputs rather than requiring code changes for ordinary environment changes.
5. Infrastructure provisioning and routine application rollout MUST follow the separate repository workflows defined by `TXV-DEL-01`.
6. Runtime behavior claims MUST be based on current code/configuration/tests/workflows/infrastructure; generated `TestResults/` output is evidence, not design authority.
7. Feature documentation MUST describe current implemented behavior; architecture and policy documentation MUST not turn historical or speculative plans into current constraints.
8. For reusable implementation practices, defer to the canonical Rule ID in [Coding Standards](coding_standards.md); this document adds only repository-specific constraints and mappings.

## Repository-specific infrastructure and delivery rules

These rules govern TransactionValidation's current Azure/GitHub delivery model. They are intentionally repository-specific and MUST NOT be presented as universal .NET deployment requirements. Their detailed workflow behavior and examples remain in the [Infrastructure and Delivery feature guide](../../features/infrastructure-and-delivery/README.md).

### TXV-DEL-01 — Separate infrastructure provisioning from application rollout

- **Applicability** - a change can update both declarative infrastructure and application images in this repository.
- **Requirement** - Bicep resource changes MUST use the infrastructure workflow. The routine application deployment workflow MUST update application images/revisions and MUST NOT reapply infrastructure on each application push.
- **Rationale** - infrastructure and application changes have different blast radius, permissions, review gates, and rollback considerations.
- **Code/workflow pattern** - see [separate infrastructure and application workflows](../../features/infrastructure-and-delivery/README.md#pattern-keep-infrastructure-and-application-delivery-in-separate-workflows).
- **Verification** - review workflow path filters, triggers, permissions, and the separation between `infra.yml` and `deploy-azure.yml`.
- **Exception process** - deviations require an approved delivery/architecture decision with scope, risk, compensating controls, and review condition.

### TXV-DEL-02 — Use federated identity for GitHub-to-Azure workflows

- **Applicability** - GitHub Actions workflows authenticate to Azure.
- **Requirement** - workflows MUST use the configured OIDC federation and least-privilege permissions for their task; long-lived Azure client secrets MUST NOT be used for workflow authentication.
- **Rationale** - short-lived federated credentials reduce stored credential exposure and bind Azure access to the trusted workflow/environment identity.
- **Code/workflow pattern** - see [OIDC for Azure access](../../features/infrastructure-and-delivery/README.md#pattern-use-oidc-for-azure-access).
- **Verification** - inspect federated identity subjects, workflow `id-token` permissions, Azure login configuration, environment protections, and assigned RBAC scope.
- **Exception process** - deviations require security and platform-owner approval and documented compensating controls; do not store long-lived credentials in source control.

### TXV-DEL-03 — Verify the deployed revision before reporting success

- **Applicability** - the application deployment workflow updates Azure Container Apps.
- **Requirement** - deployment MUST resolve resource names from the active infrastructure deployment, wait for an active running revision, and probe the API health endpoint before reporting success.
- **Rationale** - a successful image push or control-plane update alone does not establish that the application revision is running and ready.
- **Code/workflow pattern** - see [resolve deployment outputs](../../features/infrastructure-and-delivery/README.md#pattern-resolve-deployment-outputs-before-app-rollout) and [verify deployment health](../../features/infrastructure-and-delivery/README.md#pattern-verify-deployment-health-before-declaring-success).
- **Verification** - inspect and exercise the deployment workflow's output resolution, revision wait, and `/healthz` check.
- **Exception process** - deviations require an approved delivery/architecture decision, explicit operational risk acceptance, and an alternative readiness check.

## Dependency direction

The repository should follow the same dependency direction across projects:

- API -> Configuration -> Core/Messaging
- Mock -> Configuration -> Messaging
- Tests -> API and service implementations
- Infrastructure -> Azure resource definitions and workflow outputs

The dependency graph is repository-specific. The reusable principle for composition and dependency inversion is [CS-HOST-01](coding_standards.md#cs-host-01-keep-executable-hosts-thin-and-explicit) and [CS-EXT-01](coding_standards.md#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters); this section records the project-level application of those principles.

## Runtime invariants

- Health checks remain accessible without a partner API key.
- Runtime broker choice is a configuration decision, not a code branching requirement in business logic.
- Idempotency state continues to be selected by the configuration model and environment-independent contract.
- Local Docker Compose is a development topology; Azure resources are a production-like deployment environment with its own security and identity model.

## Related documentation

- [documentation_standards.md](../documentation/documentation_standards.md) — permanent authoring rules and documentation boundary policy
- [../../features/runtime-and-api/README.md](../../features/runtime-and-api/README.md)
- [../../features/messaging/README.md](../../features/messaging/README.md)
- [../../features/distributed-state/README.md](../../features/distributed-state/README.md)
- [../../features/infrastructure-and-delivery/README.md](../../features/infrastructure-and-delivery/README.md)
- [../../operations/deployment/azure_deployment/README.md](../../operations/deployment/azure_deployment/README.md)
