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

## Current stable baseline (summary)

- `ci.yml`
  - Triggers: push to `main` and `feature/**`, PR to `main`
  - Unit-test filter: `Category!=Integration`
- `integration.yml`
  - Triggers: push to `main`, PR to `main`, manual `workflow_dispatch`
  - Integration filter: `Category=Integration`
  - Uses `environment: integration` for optional protection gates
