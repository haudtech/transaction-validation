# Test Execution and Coverage Guide

This guide explains the repository test tasks, their intended scope, and the generated report locations.

## Quick Choice

Use the smallest task that answers the question:

| Goal | Task |
|---|---|
| Format, build, and complete coverage workflow | `quality:full` |
| Unit tests only | `test:unit` |
| Integration tests only | `test:integration` |
| Unit coverage report | `test:coverage:unit:full` |
| Integration coverage report | `test:coverage:integration:full` |
| Combined unit and integration coverage | `test:coverage:full` |
| Integration TRX and Markdown result report | `test:integration:trx` |
| Docker-backed E2E smoke tests | `test:e2e` |

## Quality Workflow

Run the complete local quality workflow from the VS Code Task runner:

```text
quality:full
```

The sequence is:

```text
format:verify -> build -> test:coverage:full
```

`quality:full` includes unit and integration coverage. It does not run E2E tests or the separate TRX report workflow.

## Unit Tests and Coverage

Run unit tests without coverage:

```text
test:unit
```

Run the complete unit coverage workflow:

```text
test:coverage:unit:full
```

The unit coverage sequence is:

```text
test:coverage:unit:clean -> test:coverage:unit -> test:coverage:unit:report
```

The unit report is filtered to business and application logic. Composition-root code, broker SDK adapters, and Mock host infrastructure are reported separately through integration or E2E workflows.

Output:

```text
TestResults/coverage/report
```

## Integration Tests and Coverage

Run integration tests without coverage:

```text
test:integration
```

Run integration coverage:

```text
test:coverage:integration:full
```

The integration coverage sequence is:

```text
test:coverage:integration:clean -> test:coverage:integration -> test:coverage:integration:report
```

Output:

```text
TestResults/coverage/integration-report
```

## Combined Coverage

Run the complete coverage workflow:

```text
test:coverage:full
```

The sequence is:

```text
test:coverage:unit:full -> test:coverage:integration:full -> test:coverage:combined:report
```

The combined report uses the same business-logic filters as the unit report and combines the latest unit and integration Cobertura files.

Output:

```text
TestResults/coverage/combined-report
```

Run `test:coverage:full` when coverage is the goal. Do not also run `test:integration:trx` unless a separate TRX/Markdown execution report is required, because both workflows execute the integration tests.

## Integration TRX Report

Run the integration tests and generate the dedicated TRX summary:

```text
test:integration:trx
```

This workflow is separate from coverage and writes to:

```text
TestResults/integration/integration-tests.trx
TestResults/integration/integration-tests-summary.md
```

Use it when test-by-test execution results are needed for review or CI artifacts.

## E2E Tests

Run the original Docker-backed E2E workflow:

```text
test:e2e
```

The sequence is:

```text
test:e2e:up -> test:e2e:run -> test:e2e:down
```

E2E tests are intentionally separate from coverage orchestration. They require Docker and validate the deployed runtime path, broker connectivity, and external service behavior.

## Report Interpretation

The project tracks coverage by test level:

- Unit coverage measures business and application logic.
- Integration coverage measures API host composition and cross-component behavior.
- E2E tests validate the real container and broker workflow.

Do not average the unit and integration percentages manually. Use `test:coverage:combined:report`, which merges the Cobertura data using line and branch totals.

## Prerequisites

Before running the tasks:

- Restore the .NET SDK selected by `global.json`.
- Restore local tools with `dotnet tool restore` when ReportGenerator is needed.
- Start Docker only for `test:e2e`.
- Docker Compose starts Redis for the API's distributed idempotency store; the API uses `redis:6379` inside the Compose network.
- Use the configured test filter and task rather than mixing result directories between workflows.

Generated files under `TestResults/` are artifacts and can be removed and regenerated safely.
