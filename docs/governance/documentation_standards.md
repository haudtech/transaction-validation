# Documentation Standards

## Purpose and authority

This document defines the repository's permanent documentation standards for feature and system documentation. It is the authoritative policy for how TransactionValidation explains current behavior, ownership boundaries, and implementation evidence.

This standard is intentionally stable. It governs the feature documentation tree and the document boundaries across the repository. It does not describe historical cleanup activity or implementation-era work.

## Documentation layers

The repository uses a clear separation of concerns across documentation layers:

1. Feature docs: describe implemented behavior and the current product contract.
2. Architecture docs: describe system composition, ownership, and cross-cutting runtime relationships.
3. Operational docs: describe setup, deployment, workflows, diagnostics, and runbooks.
4. Historical or implementation records: not part of the active documentation set.

The rule is simple: a document should live in the layer that owns the concern it explains.

## Source of truth

The active documentation set must remain rooted in current implementation evidence, not historical plans or cleanup records.

The following are authoritative sources for current claims:

- application code and configuration
- test suites and validation rules
- workflow definitions and infrastructure definitions
- runtime compose files and deployment manifests
- stable supporting architecture documents

Generated outputs, temporary evidence, and implementation-era artifacts are not permanent design authority.

## Feature documentation model

The authoritative feature docs live under `docs/features/` and each feature folder contains one primary document.

Each feature document MUST follow the same structure:

1. Purpose and scope
2. Behavior inventory
3. Technologies and techniques
4. Definitions and semantics
5. Implementation pattern rules
6. Configuration contract
7. Source ownership
8. Verification surface
9. Operational boundaries
10. Related supporting documents

The feature document should explain the behavior that is currently implemented, not what was proposed, planned, or later superseded.

## Ownership boundaries

Each feature document owns one clear area and must not duplicate another document's responsibility.

| Feature document | Owns | Does not own |
|---|---|---|
| `runtime-and-api` | Host behavior, endpoint contract, API discovery | Validation logic, resilience internals, Azure identity |
| `validation-and-security` | Request validation, API-key boundary, correlation validation, error formats | Endpoint orchestration, deployment operations |
| `reliability` | Retry, timeout, circuit-breaker, idempotency outcomes, failure recovery | Redis implementation details, Azure deployment procedures |
| `messaging` | Broker contract, publication, routing, consumers, redelivery | Broker provisioning and cloud deployment |
| `distributed-state` | In-memory and Redis behavior, TTL, concurrency, replay state | General retry policy or Azure provisioning |
| `observability` | Logs, correlation, traces, timing, health signals, telemetry boundaries | Dashboard design, alerts, SLOs, operational command guides |
| `testing-and-quality` | Assurance model, quality gate intent, coverage policy | Command-heavy runbooks and generated reports |
| `local-platform` | Local runtime topology and developer model | Production Azure runtime semantics |
| `azure-platform` | Azure resource model, deployment resources, identity and networking | Workflow procedures and runtime feature behavior |
| `infrastructure-and-delivery` | CI/CD lifecycle, OIDC trust, deployment boundaries | Feature behavior and day-two operations |

## Required feature content rules

Every feature document MUST:

- define a stable scope
- list current behavior explicitly
- map behavior to technologies and techniques
- define relevant semantics and boundaries
- provide implementation patterns with evidence
- document configuration contract without secrets
- identify the owning source files or project boundaries
- reference the verification surface that proves the behavior
- describe operational limits and non-goals
- link to related architecture or operational docs without duplicating them

## Pattern authoring rules

Implementation patterns are required for each behavior described in a feature document. Patterns MUST be derived from current code, configuration, tests, workflows, or infrastructure definitions.

Rules for patterns:

- Prefer interfaces, registration shapes, control-flow structure, and configuration contracts over copying full production classes.
- Use neutral placeholders instead of secrets, live resource names, or environment-specific values.
- Distinguish mandatory invariants from recommendations.
- State whether a pattern is broker-neutral, broker-specific, environment-specific, or cross-cutting.
- Keep templates minimal and compilable when practical.
- Keep operational procedures out of template code and link to the owning runbook instead.
- When an implementation is intentionally transitional, document it as a boundary rather than a recommended template.

## Evidence-first rule

Any claim in a feature document must be traceable to current repository evidence. If it is not clearly evidenced by code, infrastructure, tests, or workflow definitions, it should not remain in the active documentation set.

Good evidence includes:

- source files and application composition
- test coverage and behavior checks
- configuration models and environment contracts
- workflow and infrastructure definitions
- stable architecture references

## No duplication rule

The repository MUST avoid duplicate authority across doc layers.

- Feature docs do not duplicate architecture diagrams.
- Feature docs do not duplicate workflow command sequences.
- Architecture docs do not duplicate operational runbooks.
- Runbooks do not redefine product behavior in detail.

When a concern belongs to a different layer, the document should reference it instead of repeating it.

## Maintenance rule

This standard is a living policy for the repository. It should be reviewed when the architecture or repository boundaries change, but it should not become a historical cleanup checklist or a project-execution log.

The active documentation set should remain focused on current truth, not prior migration or cleanup activities.

## Related documentation

- [README.md](../README.md)
- [repository_architecture_rules.md](repository_architecture_rules.md)
- [../README.md](../README.md)
- [../features/README.md](../features/README.md)
