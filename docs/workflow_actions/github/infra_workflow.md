# Infrastructure Workflow Guide (`.github/workflows/infra.yml`)

Purpose: apply rare, deliberate changes to Azure infrastructure defined in `infra/bicep`. Kept separate from [deploy_azure_workflow.md](deploy_azure_workflow.md), which runs on every app-code push and must never re-apply infrastructure.

## 1) Command line-by-line explanation

### Trigger conditions
Configuration:
```yaml
on:
  pull_request:
    paths:
      - 'infra/bicep/**'
  push:
    branches: [main]
    paths:
      - 'infra/bicep/**'
  workflow_dispatch:
```
Explanation:
- PRs that touch `infra/bicep/**` get a read-only preview (what-if).
- Merges to `main` touching `infra/bicep/**` apply the changes.
- `workflow_dispatch` allows explicit recovery/repair runs.

### Environment variables
Configuration:
```yaml
env:
  LOCATION: eastus
  INFRA_DEPLOYMENT_NAME: txv-infra-dev
```
Explanation:
- `INFRA_DEPLOYMENT_NAME` is a stable, predictable deployment name — `deploy-azure.yml` queries the same name to resolve resource names from outputs.

### Job: what-if (PR preview)
Condition:
```yaml
if: github.event_name == 'pull_request'
```
Explanation:
- Runs only on PRs; preview-only and safe to run automatically.

Step: Preview infrastructure changes:
```bash
az deployment sub what-if \
  --location ${{ env.LOCATION }} \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.dev.json \
  --parameters apiKeySecretValue="${{ secrets.API_KEY_SECRET_VALUE }}"
```
Explanation:
- `az deployment sub what-if`: shows what a subscription-scoped deployment *would* change, without changing anything.
- `--template-file` / `--parameters`: the Bicep entry point and the dev parameter file.
- `apiKeySecretValue` is passed from repository secrets so secret values are never committed.

### Job: apply (real change)
Condition:
```yaml
if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'
environment: azure-infra
```
Explanation:
- Runs only on merges to `main` or explicit manual dispatch — never on PRs.
- Uses the `azure-infra` environment, which should have **required reviewers** enabled in repo Settings → Environments so every infra change gets human approval.

Step: Deploy infrastructure:
```bash
az deployment sub create \
  --name ${{ env.INFRA_DEPLOYMENT_NAME }} \
  --location ${{ env.LOCATION }} \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.dev.json \
  --parameters apiKeySecretValue="${{ secrets.API_KEY_SECRET_VALUE }}"
```
Explanation:
- `az deployment sub create`: creates/updates the subscription-scoped deployment under the stable name.
- Bicep modules under `infra/bicep/modules/` are compiled and applied by the same command.

Both jobs authenticate with the same OIDC login (`azure/login@v2` with `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` secrets) used by the deploy workflow.

## 2) When it is triggered

Defined triggers in `infra.yml`:

| Event | Paths | Job that runs |
|---|---|---|
| `pull_request` | `infra/bicep/**` | `what-if` (preview only) |
| `push` to `main` | `infra/bicep/**` | `apply` (real change, environment-gated) |
| `workflow_dispatch` | any | `apply` (environment-gated) |

Practical trigger examples:
- Open a PR changing a Bicep module => what-if runs, reviewers see the diff.
- Merge that PR => apply runs after environment approval.
- Drift or failed previous apply => run manually via dispatch.

## 3) How to trigger it manually

### GitHub UI
1. Open repository on GitHub.
2. Go to **Actions**.
3. Select **Infrastructure (Bicep)** workflow.
4. Click **Run workflow**.
5. Choose branch/ref and run.
6. Approve the `azure-infra` environment when prompted.

### GitHub CLI
```bash
gh workflow run infra.yml --ref main
```

### Re-run existing run
From the workflow run page:
- Click **Re-run all jobs** (or re-run failed jobs).

## 4) Related documentation

- OIDC prerequisite setup: [azure_deployment/oidc_prerequisite_setup.md](../../azure_deployment/oidc_prerequisite_setup.md)
- Azure deployment plan: [implementation/azure_deployment_plan.md](../../implementation/azure_deployment_plan.md)
- Validation and shutdown runbook: [azure_deployment/validation_and_shutdown_runbook.md](../../azure_deployment/validation_and_shutdown_runbook.md)
