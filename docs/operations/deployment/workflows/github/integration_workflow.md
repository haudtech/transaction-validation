# Integration Workflow Guide (`.github/workflows/integration.yml`)

This is the repository's dedicated integration-host gate. It is separate from the CI unit workflow and intentionally exercises tests tagged as `Category=Integration`.

## Trigger and scope

The workflow runs on:

- push to `main`
- pull request to `main`
- manual `workflow_dispatch`

It defines a concurrency group and cancels stale runs for the same branch or PR.

## What it validates

The pipeline does the following in order:

1. checkout the repo
2. install the .NET 8 SDK and restore packages
3. `dotnet build --no-restore --configuration Release`
4. run `dotnet test ... --filter "Category=Integration"`

This is the repository gate for host-level integration behavior and API composition tests that require the real startup and middleware pipeline, but do not depend on the local Docker-friendly E2E path.

## Environment boundary

The job uses `environment: integration`, which allows repository environment protections to enforce manual review or wait rules if the project requires them.

## Manual triggers

The workflow supports manual execution from the GitHub Actions UI or via:

```bash
gh workflow run integration.yml --ref main
```

## Related docs

- [ci_workflow.md](ci_workflow.md)
- [testing-and-quality.md](../../features/testing-and-quality/README.md)
- [../../docs/README.md](../../README.md)
