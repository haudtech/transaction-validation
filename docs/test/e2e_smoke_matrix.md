# E2E Smoke Matrix (Runtime Confidence Layer)

## Objective

Define a minimal automated E2E layer that complements existing integration-host tests without duplicating broad scenario coverage.

The integration-host suite already validates most behavior semantics in-memory. This E2E layer focuses on runtime risks and the local multiple-consumer POC:

- container startup and readiness
- real HTTP/network path
- environment wiring and ports
- service-to-service connectivity
- real broker wiring path

## Scope Policy

Keep the original runtime smoke set small; the local POC adds focused broker cases:

- 5 critical-path smoke cases
- 3 focused multiple-consumer cases
- no broad duplication of all integration-host scenarios
- assertion depth is contract-level (status, content type, key payload fields)

Primary behavior depth remains in integration-host tests.

## E2E Smoke Matrix

| E2E Case | Runtime Risk Covered | Manual HTTP Mapping | Expected Result | Keep In Integration-Host Too? |
|---|---|---|---|---|
| E2E-01: Root/health contract with API key | app boot, routing, auth middleware in deployed runtime | root | 200 OK | No (optional) |
| E2E-02: Happy path transaction accepted | API + mock + messaging path in real runtime | createAccepted | 202 Accepted | Yes |
| E2E-03: Duplicate idempotency via header | idempotency replay behavior under real network/runtime | duplicateByHeaderSamePayload | 202 Accepted (cached replay) | Yes |
| E2E-04: Missing API key | auth enforcement in deployed boundary path | missingApiKey | 401 Unauthorized | Yes |
| E2E-05: Validation failure | model validation and error contract in deployed runtime | invalidPayload | 400 ProblemDetails | Yes |
| E2E-06: Independent queue fan-out | one publication reaches both consumer queues | multiple consumer fan-out | same message and correlation IDs in both queues | No |
| E2E-07: Selective routing | accepted-only audit binding excludes unverified messages | selective routing | primary queue only for unverified message | No |
| E2E-08: Consumer redelivery | failure before acknowledgement preserves delivery | audit redelivery | audit observes a redelivered message | No |

Notes:

- These cases intentionally overlap with integration-host behavior checks.
- Overlap is acceptable because failure surfaces differ: in-memory app wiring vs real runtime boundary conditions.

## Additional Manual Scenarios (Keep Manual First)

The following scenarios in the manual script can remain manual initially and be promoted later only if recurring runtime regressions occur:

- duplicateByHeaderDifferentPayload
- duplicateWithoutHeaderSamePayload
- duplicateWithoutHeaderDifferentPayload

Source script:

- tests/TransactionValidation.Tests/Integration/http/transaction_validation_api_manual.http

Implemented automated smoke test class:

- tests/TransactionValidation.Tests/E2E/TransactionValidationE2ESmokeTests.cs

Runtime configuration knobs for automation:

- E2E_API_HOST (default: `http://localhost:${API_HOST_PORT or 5000}`)
- E2E_API_KEY (fallbacks: `SECURITY__APIKEY`, then `local-dev-api-key`)

## Execution Strategy

### Pull Request Gate

- Run unit + integration-host only (fast, deterministic).

### Merge/Nightly Runtime Gate

- Run E2E smoke matrix against real services.

Recommended cadence:

- main-branch merge: E2E smoke
- nightly: E2E smoke plus optional expanded set

## Task Naming

E2E-specific tasks are configured in the workspace with this shape:

- test:e2e:up
- test:e2e:run
- test:e2e:down
- test:e2e (dependsOn sequence: up -> run -> down)

Suggested commands:

- test:e2e:up: docker compose up -d --build
- test:e2e:run: dotnet test tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj --filter Category=E2E --results-directory TestResults/e2e --logger trx;LogFileName=e2e-tests.trx
- test:e2e:down: docker compose down -v

## Reporting Strategy

Keep E2E reporting separate from integration-host reporting:

- integration-host report: TestResults/integration/integration-tests-summary.md
- e2e report target: TestResults/e2e/e2e-tests-summary.md

Do not merge these categories into a single summary file.

## Definition of Done for E2E Introduction

- 8 smoke cases implemented and tagged Category=E2E
- tasks added for up/run/down/sequence
- CI job added for merge or nightly E2E run
- TRX and markdown summary artifacts published
- integration-host suite remains the primary PR gate

The multiple-consumer cases are local POC coverage. They use separate durable queues and do not represent Azure Function deployment.
