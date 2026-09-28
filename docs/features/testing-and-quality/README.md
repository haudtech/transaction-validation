# Testing and Quality

## Purpose and Scope

This document defines the implemented assurance model for TransactionValidation. It covers unit, integration-host, and end-to-end test responsibilities; coverage collection and reporting; formatting and build checks; Codecov policy; and generated test evidence.

It does not provide step-by-step task commands, preserve current pass counts, or treat generated reports as permanent documentation. Execution procedures remain in the supporting test guide.

## Behavior Inventory

- Unit-test selection and isolation
- Integration-host verification
- Docker-backed end-to-end verification
- Test category separation
- Unit and integration coverage collection
- Combined local coverage reporting
- CI unit coverage and Codecov status policy
- Formatting and build quality checks
- Integration TRX-to-Markdown reporting
- Generated-artifact lifecycle

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Unit testing | xUnit, Moq, FluentAssertions | Fast contract and behavior tests with external dependencies replaced | Unit test folders | `test:unit` and CI unit step |
| Integration testing | ASP.NET Core `WebApplicationFactory` | Execute the real API host and middleware with selected dependency overrides | Integration test factory and host tests | `test:integration` and integration workflow |
| E2E testing | xUnit, Docker Compose, real broker/client SDKs | Exercise built containers, HTTP boundaries, Redis, routing, consumers, and redelivery | E2E fixture and smoke tests | `test:e2e` task |
| Test separation | xUnit traits and `dotnet test` filters | Integration/E2E opt-in; unit selection excludes both categories | Tests, tasks, workflows | Task/workflow filter review |
| Coverage collection | Coverlet collector | Separate unit and integration Cobertura runs | Runsettings and tasks | Coverage tasks and XML artifacts |
| Coverage reporting | ReportGenerator | HTML, Markdown summary, and text reports; combined line/branch totals | Local tool manifest and tasks | Unit/integration/combined report tasks |
| Coverage policy | Codecov | Filtered project target and patch comparison | `codecov.yml`, CI upload | Codecov status checks when service prerequisites exist |
| Quality orchestration | VS Code tasks and GitHub Actions | Ordered format, build, test, and coverage workflows | `.vscode/tasks.json`, CI workflows | `check`, `quality:full`, CI, integration workflow |
| Test reporting | TRX and `TestReportGenerator` | Rebuild trait metadata from source and generate a readable integration summary | Report tool and integration TRX tasks | `test:integration:trx` |

## Definitions and Semantics

### Unit tests

The repository does not require a `Category=Unit` trait. Unit selection is the exclusion filter:

```text
Category!=Integration&Category!=E2E
```

Unit tests verify behavior without requiring Docker or a live broker. Current coverage includes validation, controller orchestration, in-memory and Redis store contracts, middleware, exception mapping, resilience registration/translation, publisher metadata, broker selection, logging contracts, observability registration, and health checks.

### Integration-host tests

Integration tests use `Category=Integration`. API host tests use `WebApplicationFactory<Program>` to execute the real startup and middleware pipeline in memory. The factory replaces selected external interfaces and forces in-memory idempotency while preserving host composition, middleware, routing, and exception handling.

Mock controller integration tests call the controller directly and verify deterministic and statistical timeout behavior. They do not start Docker.

### End-to-end tests

E2E tests use `Category=E2E` and are serialized through the `E2E` xUnit collection. The fixture calls running API and Mock services over HTTP and publishes directly through the selected broker SDK for routing-specific scenarios.

The repository's `test:e2e` task:

1. builds and starts Docker Compose services
2. runs the E2E category
3. tears down containers, network, and volumes

The default Compose mode uses RabbitMQ. Azure Service Bus execution requires external broker configuration and reachable API/Mock endpoints. No GitHub Actions E2E workflow exists in the repository.

### Coverage levels

Both runsettings files configure the XPlat Code Coverage collector to produce Cobertura data. Test filters, result directories, and report exclusions are defined by tasks and Codecov configuration rather than by the runsettings files.

| Coverage view | Input | Purpose |
|---|---|---|
| Unit | Unit-filtered test run | Measure filtered business and application behavior without integration/E2E categories. |
| Integration | Integration-filtered test run | Measure API host, middleware, and cross-component execution. |
| Combined local | Latest unit and integration Cobertura files | Merge line and branch totals into one local report. |
| Codecov | CI unit Cobertura upload | Apply repository path exclusions and project/patch status policy. |

The combined local report and Codecov report are not identical products. CI currently uploads only the unit coverage run to Codecov; integration coverage is collected only by the local coverage workflow.

### Coverage policy

`codecov.yml` configures:

- project target: 80%
- threshold: 3%
- patch target: automatic comparison
- exclusions for Mock, composition roots, selected broker SDK adapters, and selected option DTOs

The CI upload step uses `fail_ci_if_error: false`, so uploader failure does not fail the build job directly. Coverage enforcement depends on Codecov posting its status and repository branch protection requiring that status.

### Quality workflows

| Workflow | Implemented sequence |
|---|---|
| Local `check` | Format verification, build, unit tests |
| Local `quality:full` | Format verification, build, unit coverage, integration coverage, combined report |
| GitHub CI | Restore, Release build, unit tests with coverage, Codecov upload, formatting verification |
| GitHub integration | Restore, Release build, integration tests |
| Local E2E | Compose up/build, E2E test run, Compose down with volumes |
| Integration TRX report | Integration test run, then Markdown report generation |

E2E is not part of `quality:full`, CI, or the integration workflow. The integration TRX workflow reruns integration tests separately from coverage.

### Generated evidence

Files under `TestResults/` and CI coverage directories are generated artifacts. They can be deleted and regenerated. Final documentation may describe their paths and interpretation but must not use a particular generated report or pass count as permanent authority.

## Implementation Pattern Rules

### Pattern: Select unit tests by excluding external categories

- **Applies to** - default local and CI unit execution; test-specific.
- **Intent** - keep the fast suite independent of integration-host and deployed-runtime requirements.
- **Rule** - unit execution MUST exclude both `Integration` and `E2E` categories. Tests requiring either boundary MUST declare the corresponding trait.
- **Approved code shape**:

```text
dotnet test ... --filter "Category!=Integration&Category!=E2E"
```

- **Avoid** - running all tests as the unit gate, relying on folder names alone for filtering, or adding external dependencies to unclassified tests.
- **Implementation references** - `test:unit`, unit coverage task, and CI unit step.
- **Verification** - task/workflow inspection and unit execution.

### Pattern: Test public behavior with deterministic dependencies

- **Applies to** - unit tests; test-specific.
- **Intent** - verify observable contracts without coupling tests to external infrastructure or random behavior.
- **Rule** - unit tests MUST remain deterministic and SHOULD replace external dependencies at stable interfaces. Assertions SHOULD target outcomes, calls across contracts, and state transitions rather than private implementation steps.
- **Approved code shape**:

```csharp
var publisher = new Mock<IMessagePublisher>();
publisher
    .Setup(value => value.PublishAsync(
        It.IsAny<TransactionEnvelope>(),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

var result = await controller.CreateAsync(request, CancellationToken.None);
result.Should().BeOfType<AcceptedResult>();
```

- **Avoid** - live broker/network calls in unit tests, random outcomes, or reflection assertions when public behavior can prove the contract.
- **Implementation references** - controller, verifier, publisher, middleware, store, and logging unit tests.
- **Verification** - `test:unit`.

### Pattern: Exercise the real host for boundary integration

- **Applies to** - API integration tests; test-specific.
- **Intent** - verify startup, dependency registration, middleware order, routing, and response translation together.
- **Rule** - host integration tests MUST use `WebApplicationFactory<Program>`, retain the real host pipeline, and replace only dependencies that would make the scenario external or nondeterministic. They MUST use `Category=Integration`.
- **Approved code shape**:

```csharp
[Trait("Category", "Integration")]
[Fact]
public async Task Request_WhenBoundaryCondition_ReturnsExpectedResponse()
{
    using var factory = new ApiHostTestFactory(
        partnerVerifier: deterministicVerifier,
        messagePublisher: deterministicPublisher);
    using var client = factory.CreateClient();

    var response = await client.SendAsync(request);

    response.StatusCode.Should().Be(expectedStatus);
}
```

- **Avoid** - replacing middleware or the entire startup pipeline, testing retry timing through the host, or omitting the integration trait.
- **Implementation references** - `ApiHostTestFactory` and API integration test classes.
- **Verification** - `test:integration` and the integration workflow.

### Pattern: Reserve E2E for runtime boundaries

- **Applies to** - container/broker E2E tests; environment-specific.
- **Intent** - detect failures that in-memory host tests cannot observe.
- **Rule** - E2E tests MUST use `Category=E2E`, real HTTP calls, and the selected real broker path. Scenarios SHOULD remain focused on startup, network/configuration wiring, accepted flow, idempotency replay, routing, independent consumers, and redelivery.
- **Approved code shape**:

```csharp
[Trait("Category", "E2E")]
[Fact]
public async Task AcceptedMessage_ReachesIndependentConsumers()
{
    var accepted = await SubmitTransactionAsync();
    var primary = await fixture.WaitForObservationAsync("primary", accepted.MessageId);
    var audit = await fixture.WaitForObservationAsync("audit", accepted.MessageId);

    primary.MessageId.Should().Be(audit.MessageId);
}
```

- **Avoid** - duplicating every unit edge case in E2E, claiming E2E is a CI gate when no workflow exists, or treating an external environment as deterministic without explicit prerequisites.
- **Implementation references** - `TransactionValidationE2ESmokeTests`, `E2ETestFixture`, Docker Compose, and E2E tasks.
- **Verification** - `test:e2e`.

### Pattern: Separate coverage collection by test level

- **Applies to** - local coverage workflows and CI unit coverage.
- **Intent** - preserve meaningful level-specific results and support a mathematically combined local report.
- **Rule** - unit and integration coverage MUST run with their own filters and result directories. Combined coverage MUST merge Cobertura inputs through ReportGenerator; percentages MUST NOT be averaged manually.
- **Approved code shape**:

```text
unit tests -> unit Cobertura
integration tests -> integration Cobertura
unit + integration Cobertura -> combined ReportGenerator output
```

- **Avoid** - mixing stale result directories, averaging percentages, including E2E TRX as line coverage, or treating runsettings as the exclusion source.
- **Implementation references** - coverage runsettings, coverage tasks, and ReportGenerator tool manifest.
- **Verification** - unit, integration, and combined coverage workflows.

### Pattern: Keep coverage policy version-controlled

- **Applies to** - Codecov project/patch checks; CI-specific.
- **Intent** - make coverage scope and thresholds reviewable with code changes.
- **Rule** - Codecov targets and exclusions MUST remain in `codecov.yml`. CI MUST upload the explicit Cobertura file set. Local report filters SHOULD remain aligned with Codecov exclusions when the reports are intended to be comparable.
- **Approved code shape**:

```yaml
coverage:
  status:
    project:
      default:
        target: 80%
        threshold: 3%
```

- **Avoid** - undocumented UI-only thresholds, uploading unrelated reports by search, or describing uploader availability as an in-process test failure.
- **Implementation references** - `codecov.yml`, CI workflow, local report tasks.
- **Verification** - configuration review and Codecov status when service prerequisites are configured.

### Pattern: Compose local quality checks in deterministic order

- **Applies to** - repository task orchestration; local tooling.
- **Intent** - fail early on formatting/build defects before producing coverage reports.
- **Rule** - composite quality tasks MUST declare dependency order. The full local quality workflow MUST run format, build, then unit/integration coverage and combined reporting.
- **Approved code shape**:

```text
format:verify -> build -> test:coverage:full
```

- **Avoid** - parallel steps that consume artifacts before they exist or describing local tasks as remote merge gates.
- **Implementation references** - `.vscode/tasks.json`.
- **Verification** - `quality:full` task.

### Pattern: Treat reports as reproducible artifacts

- **Applies to** - coverage, TRX, and generated Markdown/HTML/text reports.
- **Intent** - keep source documentation stable while allowing detailed execution evidence to be regenerated.
- **Rule** - generated reports MUST live under designated artifact directories and MUST NOT be authoritative final documentation. The integration Markdown report MUST be produced from its TRX input through `TestReportGenerator`.
- **Approved code shape**:

```text
integration test run -> integration-tests.trx
integration-tests.trx -> TestReportGenerator -> integration-tests-summary.md
```

- **Avoid** - hard-coded pass counts in feature docs, manually editing generated summaries, or linking one generated report as permanent proof.
- **Implementation references** - integration TRX tasks and `tools/TestReportGenerator`.
- **Verification** - `test:integration:trx` and regenerated output inspection.

## Configuration Contract

| Surface | Implemented contract |
|---|---|
| Unit filter | `Category!=Integration&Category!=E2E` |
| Integration filter | `Category=Integration` |
| E2E filter | `Category=E2E` |
| Coverage format | Cobertura through XPlat Code Coverage |
| Unit coverage input | Unit-filtered run using `coverage.unit.runsettings` |
| Integration coverage input | Integration-filtered run using `coverage.integration.runsettings` |
| Report formats | HTML, Markdown summary, and text summary |
| Codecov project policy | 80% target with 3% threshold |
| Codecov patch policy | Automatic target |
| E2E execution | Docker Compose task; not configured as a GitHub Actions workflow |

## Source Ownership

| Surface | Responsibility |
|---|---|
| `tests/TransactionValidation.Tests/Unit` | Fast behavior and contract tests. |
| `tests/TransactionValidation.Tests/Integration` | Host pipeline and Mock controller integration tests. |
| `tests/TransactionValidation.Tests/E2E` | Real HTTP, container, broker, routing, and redelivery tests. |
| `.vscode/tasks.json` | Local task composition, filters, result directories, and reporting. |
| Coverage runsettings | Cobertura collector format. |
| `codecov.yml` | CI coverage scope and status policy. |
| CI and integration workflows | Remote unit/coverage/format and integration gates. |
| `tools/TestReportGenerator` | Integration TRX-to-Markdown conversion. |

## Verification Surface

| Verification | Proves |
|---|---|
| `test:unit` | Unit-selected tests pass without integration/E2E categories. |
| `test:integration` | Integration-tagged tests pass. |
| `test:e2e` | Docker-backed runtime scenarios pass and the stack is torn down on successful sequence completion. |
| `test:coverage:unit:full` | Unit Cobertura collection and filtered report generation. |
| `test:coverage:integration:full` | Integration Cobertura collection and report generation. |
| `test:coverage:full` | Ordered unit/integration collection and combined report generation. |
| `test:integration:trx` | Integration TRX generation and Markdown conversion. |
| CI workflow | Release build, unit coverage upload, and format verification. |
| Integration workflow | Release build and integration category execution. |

## Operational Boundaries

- No GitHub Actions E2E workflow is present; E2E is not an automated PR, merge, or nightly gate.
- `quality:full` does not run E2E or integration TRX reporting.
- CI uploads unit coverage only; it does not upload the local combined report.
- Codecov upload failure does not fail the CI job directly because `fail_ci_if_error` is false.
- Codecov merge enforcement depends on the external service posting checks and branch protection requiring them.
- Coverage exclusions are applied by ReportGenerator and Codecov configuration, not by the runsettings files.
- E2E produces TRX output but no E2E Markdown report task is configured.
- If the E2E run task fails, task-runner behavior may prevent the dependent teardown task from running; manual Compose cleanup can be required.
- Azure Service Bus E2E requires a real namespace and externally reachable test endpoints; the default local task validates RabbitMQ.
- Generated artifacts under `TestResults/` are disposable and may be stale until regenerated.

## Related Supporting Documents

- [Test execution and coverage guide](../../operations/testing/test/test_execution_and_coverage_guide.md)
- [E2E runtime matrix](../../operations/testing/test/e2e_runtime_matrix.md)
- [Runtime and API](../runtime-and-api/README.md)
- [Messaging](../messaging/README.md)
