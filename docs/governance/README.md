# Repository Governance

This folder holds the repository's durable policy, standards, and architectural guardrails. It is intentionally separate from feature behavior, runtime runbooks, and operational procedures.

## Documentation

- [Documentation standards](documentation/documentation_standards.md): the detailed authority for ownership boundaries, evidence requirements, duplication avoidance, and authoring rules.
- [Engineering documentation standard](documentation/engineering_documentation_standard.md): the short summary of the repository's default documentation model and layer ownership boundaries.
- [Documentation templates and scaffolding checklist](documentation/documentation_templates.md): reusable authoring templates and repo scaffolding guidance for consistent documentation creation.

## Coding

- [Coding standards](coding/coding_standards.md): repository expectations for structure, boundaries, and implementation quality.
- [Repository architecture rules](coding/repository_architecture_rules.md): repository-specific dependency direction, project boundaries, runtime invariants, and engineering constraints.

## Configuration

- [Configuration and environment reference](configuration/configuration_and_environment.md): environment contracts, runtime assumptions, and repo-wide configuration conventions.
- [Repository configuration reference](configuration/repository_configuration_reference.md): root-level build, SDK, formatting, and coverage configuration for the repository as a whole.

## Quality

- [Quality policy](quality/quality_policy.md): expected quality gates, evidence requirements, and validation expectations.
- [Coverage policy](quality/coverage_policy.md): coverage model and intent for unit, integration, and runtime confidence signals.

## Feature delivery

- [Feature delivery workflow](feature_delivery_workflow.md): requirement-to-implementation workflow, governance applicability, verification, documentation, definition of done, and exception handling.

## Ownership boundary

- Feature behavior belongs under [docs/features](../features/README.md).
- Architecture and runtime relationships belong under [docs/architecture](../architecture/README.md).
- Operations and delivery guidance belong under [docs/operations](../operations/README.md), including deployment, diagnostics, and test execution workflows.
- Supporting reference material belongs under [docs/reference](../reference/README.md) when it is config-, environment-, or repository-wide in nature.

This is the governance layer for how the repository documents and maintains the system, not the place for feature-level implementation detail.
