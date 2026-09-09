# Codecov Configuration Guide (`codecov.yml`)

Purpose: documents the repository-level Codecov configuration at the repo root, which controls how uploaded coverage is scoped, gated, and reported on pull requests. This file is the single source of truth for the coverage gate — thresholds live here in version control, not in the GitHub or Codecov UI.

## How it fits in the pipeline

1. [ci.yml](../../../.github/workflows/ci.yml) runs unit tests with coverage (Cobertura format, via `coverage.unit.runsettings`) and uploads the report to Codecov.
2. Codecov applies `codecov.yml`: ignores excluded paths, computes project/patch coverage, posts status checks (`codecov/project`, `codecov/patch`) and the PR comment.
3. The branch ruleset requires the `codecov/project` check, so PRs below the coverage bar cannot merge.

## Section-by-section explanation

### `ignore`

```yaml
ignore:
  - "src/TransactionValidation.Mock"
  - "**/Program.cs"
  - "src/TransactionValidation.Messaging/RabbitMqClientAdapter.cs"
  - "src/TransactionValidation.Messaging/RabbitMqTopologyInitializer.cs"
  - "src/TransactionValidation.Messaging/ServiceBusClientFactory.cs"
  - "src/TransactionValidation.Messaging/ServiceBusMessageSender.cs"
  - "src/TransactionValidation.Configuration/Options/RabbitMqOptions.cs"
  - "src/TransactionValidation.Configuration/Options/SerilogOptions.cs"
```

- Paths excluded from coverage accounting entirely — they count as neither covered nor missed.
- The list mirrors the assembly/class filters in the local report tasks (`.vscode/tasks.json`, `test:coverage:unit:report`), so the Codecov percentage matches the locally measured one (~87.5% vs the raw ~40.5% that includes the Mock server).
- What is excluded and why:
  - `TransactionValidation.Mock` — exists only to be called over HTTP by integration/E2E tests; has no unit tests by design and would dominate the total.
  - `Program.cs` — composition root / startup wiring; validated by host integration tests.
  - Messaging client/factory/sender/topology classes — thin wrappers over RabbitMQ/Service Bus SDKs that need live brokers; covered by integration tests.
  - Options DTOs — pure data containers with no logic.
- When adding a new exclusion, update the local task filters too (and vice versa) so both numbers stay aligned.

### `coverage.status`

```yaml
coverage:
  status:
    project:
      default:
        target: 80%
        threshold: 3%
    patch:
      default:
        target: auto
```

- `project` — total coverage of the whole (non-ignored) codebase at the PR head. `target: 80%` fails the `codecov/project` check below 80%; `threshold: 3%` allows a 3-point miss against the target before failing (absorbs minor fluctuations from refactors). This is the check the branch ruleset requires.
- `patch` — coverage of only the lines changed in the PR. `target: auto` compares against the base commit; it is informational pressure to test new code, not the blocking gate.

### `comment`

```yaml
comment:
  layout: "reach, diff, files"
  require_base: false
  require_head: true
```

- Controls the Codecov PR comment: which sections are shown, and when it posts. `require_base: false` lets it comment on the first-ever upload (no baseline yet); `require_head: true` means no comment if the PR's own upload is missing.

## Validation

Codecov provides an official validator — run it after every change to this file:

```bash
curl -s -X POST --data-binary @codecov.yml https://codecov.io/validate
```

Expected output starts with `Valid!` and echoes the parsed configuration (useful to confirm patterns matched as intended).

## Prerequisites and failure modes

| Requirement | What breaks without it |
|---|---|
| `CODECOV_TOKEN` repo secret passed to `codecov/codecov-action@v5` | Uploads succeed but no status checks are posted (log shows `Token length: 0`) |
| Codecov GitHub App installed on the repo | Status checks still not posted; Codecov can only comment (its PR comment warns about this) |
| Baseline report on `main` | First PRs show coverage but comparisons/diffs are unavailable until `main` has a report |

Full diagnostic walkthrough: case study 6 in [workflow_case_studies.md](workflow_case_studies.md).

## Related docs

- CI pipeline that produces and uploads the report: [ci_workflow.md](ci_workflow.md)
- Merge-gate wiring (`codecov/project` as required check): [branch_protection_setup.md](branch_protection_setup.md)
- Local coverage tasks and report filters: `.vscode/tasks.json` (`test:coverage:unit:full`, `test:coverage:combined:report`)
