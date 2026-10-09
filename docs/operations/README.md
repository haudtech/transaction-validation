# Operations Documentation

This folder is the dedicated operational layer for TransactionValidation. It contains guidance for running, validating, diagnosing, and supporting the system in local and deployed environments.

## Scope

Operational docs answer how to:

- start and validate the system locally
- deploy and verify infrastructure in Azure
- run test suites and coverage workflows
- diagnose runtime and integration issues
- maintain healthy delivery and shutdown procedures

## Subfolders

- [deployment](deployment/README.md): Azure deployment, identity, resource provisioning, release workflows, and environment operations.
- [local](local/README.md): local development and runtime guide for Compose/native startup, debugging, validation, logs, and cleanup.
- [testing](testing/README.md): unit, integration, E2E, and coverage execution guidance.
- [diagnostics](diagnostics/README.md): health checks, troubleshooting, logs, and runtime investigation guidance.

## Ownership boundary

This layer is intentionally separate from:

- feature behavior in [docs/features](../features/README.md)
- system architecture in [docs/architecture](../architecture/Architecture_design.md)
- repo governance and authoring standards in [docs/governance](../governance/README.md)
- repository-wide reference material in [docs/reference](../reference/README.md)

Operational docs should describe procedures and environment concerns, not feature behavior or long-term system design.
