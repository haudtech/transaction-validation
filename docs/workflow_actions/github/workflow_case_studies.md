# Workflow Case Studies (Fixed Issues)

This document captures real workflow failures that occurred in this repository and the final fixes that stabilized CI.

## Case Study 1: Node 20 deprecation warning in GitHub Actions

### Symptoms
- CI annotations showed warnings similar to:
  - `Node.js 20 is deprecated... actions/cache@v4, actions/checkout@v4, actions/setup-dotnet@v4 ...`

### Root cause
- Older major versions of actions were being used and routed through a deprecated runtime path on GitHub-hosted runners.

### Fix applied
- Updated actions to current versions:
  - `actions/checkout@v5`
  - `actions/setup-dotnet@v5`
- Removed explicit `actions/cache@v4` step and used built-in cache in `setup-dotnet`.

### Preventive guidance
- Keep core actions on current major versions.
- Prefer built-in caching when the setup action supports it.

---

## Case Study 2: VSTest error - "The argument ...TransactionValidation.Tests.dll is invalid"

### Symptoms
- Test step failed quickly with output like:
  - `The argument .../TransactionValidation.Tests.dll is invalid`
  - `MSB4181: The "VSTestTask" task returned false but did not log an error.`

### Root cause
- Test invocation was brittle on runner/VSTest path selection and adapter resolution.
- Workflow test command style increased chance of VSTest target edge cases.

### Fix applied
- Made test project definition explicit and stable:
  - Added `<IsTestProject>true</IsTestProject>` in `tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj`.
  - Added standard metadata for `xunit.runner.visualstudio`:
    - `<PrivateAssets>all</PrivateAssets>`
    - `<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`
- Simplified and hardened workflow test commands:
  - Run tests by explicit project path (not ambiguous solution target):
    - `dotnet test tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj ...`
  - Use consistent build config in test step:
    - `--configuration Release`
  - Removed `--no-build` from test steps to avoid no-build testhost edge cases.

### Preventive guidance
- Prefer explicit test project path in CI.
- Keep test SDK/adapter settings explicit in test project.
- Avoid over-optimizing test commands with `--no-build` unless proven stable.

---

## Case Study 3: CI observability and diagnostics quality

### Symptoms
- Early failures were hard to triage due to missing environment details in logs.

### Root cause
- Workflows did not print SDK/runtime info and formatting checks had limited diagnostics.

### Fix applied
- Added:
  - `dotnet --info` in both workflows.
- Improved format step in CI:
  - `dotnet format TransactionValidation.sln --verify-no-changes --verbosity diagnostic`

### Preventive guidance
- Always include at least one environment diagnostic step in build/test workflows.
- Use diagnostic verbosity for gates that often fail in CI but pass locally.

---

## Case Study 4: Required status checks stuck on "Expected — Waiting for status to be reported"

### Symptoms
- PR showed all checks green (`CI / build (pull_request)`, `Integration Tests / integration (pull_request)` succeeded), yet the required entries stayed pending forever and merging remained blocked.

### Root cause
- The branch ruleset's required status check contexts were configured with PR-UI display strings (`CI / build (pull_request)`) instead of the actual check-run names GitHub Actions reports — the job names `build` and `integration`.
- A ruleset requires an exact context match; the display string in the Checks tab is `<workflow name> / <job name> (<event>)`, which is not the context.

### Fix applied
- Updated the ruleset's `required_status_checks` contexts to the job names (`build`, `integration`) via the API, then re-ran the checks. The required entries matched and went green.

### Preventive guidance
- When configuring required checks, copy the context from the check-run `name` field, not the Checks tab display:
  ```bash
  gh api repos/<owner>/<repo>/commits/<head-sha>/check-runs \
    --jq '.check_runs[] | {name, status, conclusion}'
  ```
- See [branch_protection_setup.md](branch_protection_setup.md) for the full diagnostic sequence.

---

## Case Study 5: "Merging is blocked — Cannot update this protected ref" with all checks green

### Symptoms
- After the required-check contexts were fixed, every check passed, but merging was still blocked with `Cannot update this protected ref.`

### Root cause
- The ruleset included the `update` rule ("Restrict updates: only allow users with bypass permission to update matching refs") while its bypass actor list was empty.
- Unlike classic branch protection, rulesets do **not** automatically bypass admins/owners — so nobody could update `main`, including merge commits. Rule evaluations confirmed: `update: fail` (`Cannot update this protected ref.`), all other rules `pass`.

### Fix applied
- Removed the `update` rule from the ruleset. PR-only merging is already enforced by the `pull_request` rule; check enforcement stays via `required_status_checks` with an empty bypass list.

### Preventive guidance
- Only add the `update` rule if you intend to gate ref updates behind an explicit bypass-actor list.
- To diagnose any blocked merge, read the rule evaluations:
  ```bash
  gh api repos/<owner>/<repo>/rulesets/rule-suites \
    --jq '.rule_suites[0:5][] | {id, result, ref: .ref}'
  gh api repos/<owner>/<repo>/rulesets/rule-suites/<id> \
    --jq '.rule_evaluations[] | {rule_type, result, details}'
  ```

---

## Current stable baseline (summary)

- `ci.yml`
  - Triggers: push to `main`, PR to `main`
  - Unit-test filter: `Category!=Integration&Category!=E2E`
  - Concurrency: per-PR/ref group, cancel-in-progress
- `integration.yml`
  - Triggers: push to `main`, PR to `main`, manual `workflow_dispatch`
  - Integration filter: `Category=Integration`
  - Uses `environment: integration` for optional protection gates
  - Concurrency: per-PR/ref group, cancel-in-progress
- `deploy-azure.yml`
  - Triggers: push to `main` (paths: `src/**`, workflow file), manual `workflow_dispatch`
  - Builds/pushes API + Mock images to ACR, updates Container Apps, verifies `/healthz`
  - Uses `environment: azure-dev` (ungated for auto-deploy-on-merge)
  - Concurrency: per-ref group, cancel-in-progress
- `infra.yml`
  - Triggers: PR touching `infra/bicep/**` (what-if preview), push to `main` touching `infra/bicep/**` (apply), manual `workflow_dispatch` (apply)
  - `apply` job uses `environment: azure-infra`, which should have required reviewers enabled
- Ruleset `main` (id `20794465`)
  - Required checks: `build`, `integration` (strict up-to-date policy)
  - PR-only merges, no bypass actors — see [branch_protection_setup.md](branch_protection_setup.md)
