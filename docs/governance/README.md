# Repository Governance

This folder holds the repository's durable policy, standards, and architectural guardrails. It is intentionally separate from feature behavior, runtime runbooks, and operational procedures.

## Policy and standards

- [Documentation standards](documentation_standards.md): permanent rules for feature and system documentation, ownership boundaries, evidence requirements, and duplication avoidance.
- [Repository architecture rules](repository_architecture_rules.md): repository-specific dependency direction, project boundaries, runtime invariants, and engineering constraints.
- [Repository configuration reference](repository_configuration_reference.md): root-level build, SDK, formatting, and coverage configuration for the repository as a whole.

## Ownership boundary

- Feature behavior belongs under [docs/features](../features/README.md).
- Architecture and runtime relationships belong under [docs/architecture_design](../architecture_design/Architecture_design.md).
- Operations and delivery guidance belong under [docs/operations](../operations/README.md), including deployment, diagnostics, and test execution workflows.

This is the governance layer for how the repository documents and maintains the system, not the place for feature-level implementation detail.
