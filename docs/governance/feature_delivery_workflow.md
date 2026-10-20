# Feature Delivery Workflow and Definition of Done

## Purpose and authority

Use this workflow to take a feature from an approved business requirement through implementation, verification, and domain-specific documentation. It makes the applicable governance rules explicit without requiring irrelevant patterns or duplicating feature behavior. For a new project that has not established its governance baseline, complete the [new project governance bootstrap](project_governance_bootstrap.md) first.

This workflow is a delivery checklist, not an architecture decision record or feature specification. The applicable governance standard owns each rule; the feature guide owns current feature behavior. See [governance authority and precedence](documentation/documentation_standards.md#governance-authority-and-precedence).

### Agenda

- [Workflow](#workflow)
- [New-project prerequisite](#new-project-prerequisite)
- [AI-assisted implementation](#ai-assisted-implementation)
- [Definition of done](#definition-of-done)
- [Exception record](#exception-record)
- [Related governance](#related-governance)

## Workflow

### New-project prerequisite

When creating a project with no established governance, follow the ordered [New Project Governance Bootstrap](project_governance_bootstrap.md) and pass its readiness gate before the first implementation change. For an established project, continue with the feature workflow below and consult the applicable authorities.

### 1. Understand and bound the requirement

Before implementation, record:

- the business outcome and actors
- observable success and rejection outcomes
- important failure, retry, duplicate, and recovery cases where relevant
- security, privacy, data-retention, availability, and performance constraints that affect the design
- scope, explicit non-goals, and dependencies

Clarify ambiguous behavior with the requirement owner before encoding assumptions in interfaces or configuration.

### 2. Select applicable governance rules

Review the applicable standards and record decisions in the feature change or its review evidence. For each applicable mandatory coding rule, record its stable rule ID and title, the approved pattern used, verification evidence, and any approved exception. Use the rule's own authority for its requirement; do not duplicate the rule text. Mark a rule **Not applicable** only with a brief reason; do not implement a pattern merely because it exists in the catalog.

| Concern | Apply when the feature... | Governing source |
|---|---|---|
| Project boundaries and dependency direction | adds or changes a project, dependency, or runtime responsibility | [repository architecture rules](coding/repository_architecture_rules.md) |
| Coding and implementation patterns | adds or changes production code | [coding standards](coding/coding_standards.md) |
| Configuration contract | introduces settings, defaults, environment-specific behavior, or secrets | [configuration and environment](configuration/configuration_and_environment.md) and the owning feature guide |
| Documentation ownership and evidence | adds or changes documentation | [documentation standards](documentation/documentation_standards.md) |
| Quality and verification | changes behavior, boundaries, or risk | [quality policy](quality/quality_policy.md) and [coverage policy](quality/coverage_policy.md) |
| External dependency resilience | makes remote calls that can fail or time out | [reliability feature guide](../features/reliability/README.md) |
| Messaging delivery | publishes or consumes broker messages | [messaging feature guide](../features/messaging/README.md) |
| Security, validation, or correlation | changes inbound trust, request validation, or identity propagation | [validation and security feature guide](../features/validation-and-security/README.md) |
| Logs, traces, metrics, or health | adds or changes operational signals | [observability feature guide](../features/observability/README.md) |

This is a selection aid, not an exhaustive inventory. Consult the governance index and the owning feature guide when a change crosses another concern.

### 3. Design ownership, contracts, and invariants

Before coding:

- identify which project/component owns the behavior and which boundaries it crosses
- define public or inter-project contracts before implementation-specific details
- state the invariants that must remain true across success and failure
- decide where configuration is bound and validated, where errors are translated, and where side effects occur
- separate request/data semantics (such as idempotent replay) from operational recovery (such as retry and timeout)
- record cross-cutting architecture decisions in the architecture authority when they affect system relationships or runtime topology

Prefer the smallest design that satisfies the requirement and existing boundaries. Do not add a new layer, abstraction, or technology without a concrete responsibility it owns.

### 4. Implement a vertical slice using applicable patterns

Implement through the owning boundary, keeping host composition, business orchestration, configuration, adapters, and infrastructure responsibilities distinct. Apply relevant MUST/MUST NOT rules from [coding standards](coding/coding_standards.md) and repository-specific constraints from [repository architecture rules](coding/repository_architecture_rules.md).

Keep the feature guide current during implementation where practical. Do not wait until the end to reconstruct behavior from code.

When an AI assistant generates or modifies implementation artifacts, it MUST follow the [AI-assisted development standard](ai-assisted-development.md) and its [reusable implementation prompt](ai-assisted-development.md#reusable-implementation-prompt). AI use does not change the governance requirements or reduce the human review obligation.

### 5. Verify behavior at the owning boundaries

Plan verification against the requirement and its invariants:

- unit tests for owned decisions and transformations
- host/integration tests for dependency registration, middleware, configuration, and cross-component behavior
- infrastructure-backed or deployed-runtime tests when the requirement depends on real topology, SDK behavior, or deployment configuration
- analyzers, format checks, build, and quality gates required by repository policy

Cover the important success and failure transitions. Prefer deterministic tests for failure cases. Choose the least costly boundary that genuinely proves the behavior; integration/E2E tests complement rather than replace focused unit tests. Use the relevant [test execution guide](../operations/testing/test/test_execution_and_coverage_guide.md) for commands and environment procedures.

### 6. Document implemented behavior

Create or update the owning domain document under `docs/features/` using the required template in [documentation templates](documentation/documentation_templates.md). It MUST describe behavior evidenced by the final implementation, link to applicable governance rules, identify configuration and source ownership, and map important claims/invariants to verification evidence.

Update architecture documentation only for cross-cutting system design or runtime relationships. Update operations documentation only when setup, deployment, diagnosis, or support procedures change. Link across layers instead of duplicating their content.

### 7. Review and complete

Use the definition-of-done checklist below. For each applicable rule, reviewers should be able to follow the evidence from the rule to implementation and verification. Resolve contradictions before merging; do not treat documentation updates as a substitute for code or tests.

## AI-assisted implementation

For AI-generated or AI-edited changes, use the [AI-assisted development standard](ai-assisted-development.md). The prompt requires the assistant to inspect repository state, identify applicable governance rules before editing, stop on unresolved conflicts, implement only the approved scope, run verification, and report evidence and limitations. Prompts guide the workflow but cannot guarantee correctness; a human reviewer must inspect the resulting diff.

## Definition of done

A feature is complete when all applicable items are satisfied or an exception is approved and recorded:

- [ ] Requirement, scope, observable outcomes, and important non-goals are clear.
- [ ] Applicable governance rules and patterns have been identified by stable rule ID/title where defined; non-applicable areas have a reason where ambiguity is likely.
- [ ] Project ownership, dependency direction, contracts, and important invariants are explicit.
- [ ] Configuration defaults, required values, validation, environment mapping, and secret handling are addressed where applicable.
- [ ] Implementation follows the applicable coding and architecture rules, or has an approved exception.
- [ ] Tests verify behavior and important success/failure invariants at the appropriate boundaries.
- [ ] Required format, build, analyzer, and quality checks pass; required test results are reviewed.
- [ ] The owning feature guide describes implemented behavior and links applicable rules to evidence.
- [ ] Architecture, configuration, or operations documentation is updated only when its owned concern changed.
- [ ] Documentation links and references point to current canonical authorities.
- [ ] Any exception includes an approver and review/expiry condition where the deviation may become temporary debt.

## Exception record

Exceptions are for justified cases where an applicable rule cannot reasonably be followed; they are not a way to bypass security, data-integrity, privacy, or explicit runtime invariants. Record an exception in the feature change/review or owning decision record, not as an undocumented local convention.

Use this record:

| Field | Required content |
|---|---|
| Rule | Stable rule ID, title, and owning governance authority. |
| Reason | Why the standard pattern is unsuitable for this requirement. |
| Scope and impact | Components affected, risks introduced, and compensating controls. |
| Approval | Responsible reviewer/owner who accepted the deviation. |
| Review condition | Expiry date, follow-up issue, or explicit statement that the exception is permanent and why. |
| Verification | Tests or operational checks that reduce the deviation's risk. |

Security, privacy, and data-integrity requirements MUST NOT be waived through an informal exception. Escalate conflicts to the appropriate security, data, or architecture owner.

## Related governance

- [Repository governance index](README.md)
- [Engineering documentation standard](documentation/engineering_documentation_standard.md)
- [Documentation standards](documentation/documentation_standards.md)
- [Quality policy](quality/quality_policy.md)
- [Test execution and coverage guide](../operations/testing/test/test_execution_and_coverage_guide.md)
