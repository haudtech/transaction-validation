# Repository Architecture Rules

## Purpose and Scope

This document records the repository-specific architecture and dependency rules that are enforced by the codebase, the configuration model, and the testing model. It is intentionally repository-specific and narrower than general engineering guidance.

The rules below describe responsibilities and boundaries that are currently verified by the code and tests in this solution.

### Agenda

- [Core project and component boundaries](#core-boundaries)
- [Architecture terminology and service boundaries](#architecture-terminology-and-service-boundaries)
- [Canonical coding patterns applied by this architecture](#canonical-coding-patterns-applied-by-this-architecture)
- [Enforced repository rules](#enforced-rules)
- [Repository-specific infrastructure and delivery rules](#repository-specific-infrastructure-and-delivery-rules)
- [Dependency direction](#dependency-direction)
- [Runtime invariants](#runtime-invariants)
- [Related documentation](#related-documentation)

## Core boundaries

### API host boundary

The `TransactionValidation.Api` project owns the HTTP host, middleware composition, health endpoints, routing, API-key enforcement, controller orchestration, and configuration registration. It does not own the implementation of the broker protocol or the external partner verification client.

### Core validation and orchestration boundary

The `TransactionValidation.Core` project owns the domain validation, data contracts, and orchestration semantics that are independent of the broker transport and hosting layer. It should remain stable under both local and Azure deployment modes.

### Core responsibility-based folder structure

Core types MUST be placed in the top-level folder that matches their primary implementation responsibility:

| Folder | Owns | Current example |
|---|---|---|
| `Enums/` | Closed domain value sets represented as enums. Keep enum member names and meaning domain-specific. | `CurrencyCode` defines the supported `USD` and `EUR` values. |
| `Parsers/` | Conversion of external representations into canonical typed values, including explicit normalization and rejection of unsupported input. | `CurrencyCodeParser.TryParse` trims and parses supported currency codes; `IsSupported` delegates to it. |
| `Validation/` | Validation rules for domain/request models and composition of parser results with other field rules. | `PartnerTransactionRequestValidator` owns request validation and calls `CurrencyCodeParser.IsSupported`. |

These folders are responsibility-based, not domain silos: keep types domain-named (`CurrencyCode`, `CurrencyCodeParser`, `PartnerTransactionRequestValidator`) and group files by what they do. Use `TryParse` when downstream behavior needs the typed value; use an `IsSupported` predicate only when a pass/fail check is sufficient. Preserve external DTO/wire types when required for API compatibility, and do not re-parse the original input in downstream logic when a parsed value is already available.

Do not use `Common/` as a general-purpose home for enums, parsers, or validators. Add a new responsibility folder only when the project has types that belong to that responsibility; do not create nested `Common/Enums`, `Common/Parsers`, or `Common/Validations` folders. Keep simple or single-purpose domain behavior in its existing owner when moving it would obscure ownership.

The Core source tree is organized by responsibility, not by C# accessibility. Do not add `Public/`, `Internal/`, or `Private/` directory branches: folders and namespaces do not enforce visibility. Use `public` only for contracts and types intentionally consumed outside the assembly, `internal` for assembly-scoped implementation types when they do not need to cross project boundaries, and `private` for implementation details at member/nested-type scope. Keep a type in the responsibility folder that owns it regardless of its access modifier. For example:

```text
TransactionValidation.Core/
├── Enums/
│   └── CurrencyCode.cs                 (public domain value set)
├── Parsers/
│   └── CurrencyCodeParser.cs           (public Core parsing contract)
├── Validation/
│   └── PartnerTransactionRequestValidator.cs
├── Models/
├── Interfaces/
├── Exceptions/
└── Logging/
```

This is an illustrative responsibility tree, not a requirement to create empty folders. Keep the public API as small as practical; prefer `internal` or private helpers when consumers outside the Core assembly do not need them. If a type must be consumed by another project, its accessibility must permit that use—the folder structure itself grants no access.

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
| [CS-VAL-01 — Parse finite external values into typed domain values](coding_standards.md#cs-val-01-parse-finite-external-values-into-typed-domain-values) | Currency request text is normalized and checked against the explicit `CurrencyCode` allowlist through `CurrencyCodeParser`; the API wire field remains a string. | [Currency request contract](../../features/validation-and-security/README.md#request-contract). |
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
9. Core enums, parsers, and domain/request validators MUST live in their respective top-level responsibility folders (`Enums/`, `Parsers/`, and `Validation/`) and MUST use domain-specific type names; `Common/` MUST NOT become a catch-all for these responsibilities.
10. Core source folders MUST reflect implementation responsibility rather than C# visibility. Visibility MUST be controlled with C# access modifiers; separate `Public/`, `Internal/`, or `Private/` folder branches MUST NOT be introduced as an access-control mechanism.

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
