# Codecov Configuration Guide (`codecov.yml`)

This guide describes the repository-level Codecov policy that complements the CI workflow. The authoritative coverage gate remains in `codecov.yml`, while the publishing step is defined in [.github/workflows/ci.yml](../../../.github/workflows/ci.yml).

## What the repository configures

The repo configures:

- project coverage target: 80%
- threshold: 3%
- path exclusions for the Mock project, selected broker wrappers, startup wiring, and DTO-only option classes
- patch coverage comparison using the Codecov default policy

These settings align the upload with the filtered unit coverage report used in the local workflow and keep the project gate consistent across CI and local reporting.

## Why exclusions exist

The excluded paths represent parts of the repository that are intentionally not unit-tested in the same way as business logic:

- `src/TransactionValidation.Mock` — exercised through HTTP and container-backed runtime tests
- `Program.cs` — composition/root validation handled by host-level integration checks
- broker adapter and topology classes — thin wrappers over live external SDKs
- `RabbitMqOptions` and `SerilogOptions` — DTO/configuration classes without domain logic

The local and CI report filters are kept aligned so the numbers remain comparable.

## Status and enforcement

Codecov posts project and patch status checks on the PR when the upload succeeds and the GitHub App is installed. The branch ruleset and repository protection settings enforce those checks as required checks before merge.

## Related docs

- [ci_workflow.md](ci_workflow.md)
- [branch_protection_setup.md](branch_protection_setup.md)
- [../../features/testing-and-quality/README.md](../../features/testing-and-quality/README.md)
