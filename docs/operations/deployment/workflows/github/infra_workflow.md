# Infrastructure Workflow Guide (`.github/workflows/infra.yml`)

This workflow is the repository's infrastructure gate. It is the only workflow that changes Azure resource definitions in `infra/bicep` and is intentionally separate from the application deployment workflow.

## Trigger model

The workflow runs on:

- pull request touching `infra/bicep/**` → preview-only `what-if`
- push to `main` touching `infra/bicep/**` → real `apply`
- manual `workflow_dispatch` → real `apply`

The `pull_request` path is a safe preview, while the `push` and `dispatch` paths do real provisioning work under the protected `azure-infra` environment.

## What the workflow does

The infrastructure workflow authenticates with OIDC and runs:

- `az deployment sub what-if` for preview validation
- `az deployment sub create` for live infrastructure updates

Both paths target `infra/bicep/main.bicep` and the dev parameter file, and both pass the `apiKeySecretValue` secret without embedding it in the repository.

## Protection boundary

The `apply` job is protected by the `azure-infra` environment and should remain gated by repository environment approvals. This prevents normal code changes from accidentally reapplying infrastructure while preserving a deliberate, reviewable path for infra edits.

## Related docs

- [deploy_azure_workflow.md](deploy_azure_workflow.md)
- [../../azure_deployment/README.md](../../azure_deployment/README.md)
- [../../features/infrastructure-and-delivery/README.md](../../features/infrastructure-and-delivery/README.md)
