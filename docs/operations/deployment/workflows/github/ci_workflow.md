# CI Workflow Guide (`.github/workflows/ci.yml`)

This guide summarizes the current repository CI gate and its relationship to the local quality tasks.

## Trigger and purpose

The workflow runs on push and pull request to `main` and uses a concurrency group to cancel stale runs for the same branch or PR.

It is the repository gate for:

- restore
- Release build
- unit test execution with coverage
- coverage upload to Codecov
- formatting verification

## Workflow behavior

The job flow is intentionally narrow and matches the local quality contract:

1. checkout the repo
2. install the .NET 8 SDK with NuGet cache enabled
3. `dotnet restore`
4. `dotnet build --no-restore --configuration Release`
5. run only unit tests with `Category!=Integration&Category!=E2E`
6. upload the Cobertura result to Codecov
7. run `dotnet format TransactionValidation.sln --verify-no-changes`

The test filter excludes the integration and E2E categories so the CI gate stays fast and deterministic. Integration-host tests remain in the dedicated integration workflow, while Docker-backed E2E tests are local runtime checks rather than CI coverage gates.

## Coverage and status policy

Coverage is generated from the unit test run and uploaded to Codecov with the repo-level settings from `codecov.yml`.

The current repository policy is:

- project target: 80%
- threshold: 3%
- upload path is the Cobertura XML produced by the unit runsettings file
- excluded paths mirror the local report filters used for the unit report task

The upload step is configured with `fail_ci_if_error: false` to avoid turning a provider-side upload issue into a false build failure. The real enforcement point remains the Codecov status check and the branch protection ruleset.

## Manual trigger and scope

The current workflow does not include `workflow_dispatch`, so manual runs are not configured here. The expected trigger paths are the repository `main` branch events only.

## Related docs

- [integration_workflow.md](integration_workflow.md)
- [codecov_configuration.md](codecov_configuration.md)
- [branch_protection_setup.md](branch_protection_setup.md)
- [../../docs/README.md](../../README.md)
