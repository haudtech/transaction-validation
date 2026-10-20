# Quality Policy

This document describes the expected quality baseline for the repository.

## Agenda

- [Required checks](#required-checks)
- [Quality gate principles](#quality-gate-principles)
- [Related docs](#related-docs)

## Required checks

- format verification
- build validation
- unit test execution
- integration validation for boundary behaviors
- coverage policy review for changed or critical areas

## Quality gate principles

- a change should be testable and verifiable
- code quality is part of the delivery contract
- architecture and behavior changes require evidence
- each feature should follow the [feature delivery workflow](../feature_delivery_workflow.md) and satisfy its definition-of-done checklist; verification must be appropriate to the changed behavior and risk

## Related docs

- [coverage_policy.md](coverage_policy.md)
- [feature_delivery_workflow.md](../feature_delivery_workflow.md)
- [../documentation/documentation_standards.md](../documentation/documentation_standards.md)
