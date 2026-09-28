# Infrastructure and Delivery

## Purpose and Scope

This document defines the implemented repository delivery model for TransactionValidation. It covers the CI pipeline for build and unit verification, the separate integration-host workflow, the infrastructure preview/apply flow for Bicep-managed Azure resources, and the application deployment workflow that updates existing Azure Container Apps after an infrastructure deployment exists.

It does not describe runtime messaging semantics, request validation behavior, or day-two Azure operations in detail. Those concerns remain in the application, messaging, Azure deployment, and workflow runbook guides.

## Behavior Inventory

- CI build and unit verification
- Integration-host execution in GitHub Actions
- Separate Azure infrastructure workflow
- Azure OIDC authentication and environment gating
- Bicep preview and apply flow
- Application image build and push to ACR
- Container App revision update and health verification
- Coverage upload and Codecov status policy
- Required-check and protected-environment boundaries
- Pipeline-to-Azure separation of responsibilities

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| CI validation | GitHub Actions, .NET SDK | Restore, build, unit tests, coverage upload, formatting verification | `.github/workflows/ci.yml` | CI workflow inspection and local task parity |
| Integration gating | GitHub Actions, xUnit filters | Dedicated `Category=Integration` workflow with a separate environment | `.github/workflows/integration.yml` | Integration workflow inspection |
| Azure identity | OIDC, Azure login | Federated identity with environment-bound deployment approvals | Azure workflow and infra workflow | Workflow YAML and deployment environment names |
| Infrastructure deployment | Azure CLI + Bicep | `az deployment sub what-if` and `az deployment sub create` | `infra/bicep/main.bicep` and infra workflow | `infra.yml` and module outputs |
| Application deployment | Docker, Azure Container Apps, ACR | Build on runner, push to ACR, then update existing app revisions | `deploy-azure.yml` | Deployment workflow inspection |
| Runtime health verification | Azure CLI, curl | Wait for active revision and probe `/healthz` | Deployment workflow | Deployment task and workflow step review |
| Coverage status | Codecov | Upload Cobertura from CI to repository status checks | `ci.yml` and `codecov.yml` | Codecov configuration and workflow upload step |
| Protected delivery | GitHub environment protections | Environment-level approvals gate infra changes | `infra.yml` environment: `azure-infra` | Workflow environment declaration |

## Definitions and Semantics

### CI workflow

The repository CI workflow runs on push and pull request to `main` and uses `concurrency` to cancel in-progress duplicate work for the same branch or PR. It restores, builds in Release mode, runs the non-integration unit suite, uploads Cobertura to Codecov, and verifies formatting with `dotnet format --verify-no-changes`.

### Integration workflow

The integration workflow is a separate delivery gate. It runs on pushes and pull requests to `main` and supports manual dispatch. It builds the solution and executes tests filtered by `Category=Integration` in a dedicated `integration` environment. This workflow is intentionally separate from the CI unit gate.

### Infrastructure workflow

The infrastructure workflow is intentionally distinct from app rollout. It watches `infra/bicep/**` changes and uses `az deployment sub what-if` for pull requests and `az deployment sub create` for push/manual runs. The current deployment name is stable and is used by the deployment workflow to resolve output values from the live Azure deployment.

### Deployment workflow

The Azure deployment workflow runs on application changes and is designed to update already-provisioned Container Apps. It authenticates to Azure with OIDC, queries the infra deployment outputs for resource names, logs in to ACR, builds the API and Mock images on the runner, pushes those images, updates the Container App revisions, and waits for at least one running revision before probing `/healthz` on the API.

### Delivery boundaries

The repository separates the concerns as follows:

- infrastructure changes are managed by the Bicep workflow
- application delivery is managed by the app deployment workflow
- CI and integration tests verify repository quality independent of Azure provisioning
- Azure operations and environment setup remain in the Azure deployment documentation rather than the workflow guide

## Configuration Contract

The workflow layer depends on a small set of repository and environment values. The values below are names, not secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `API_KEY_SECRET_VALUE`
- `CODECOV_TOKEN`

These values are consumed by environment-specific and workflow-specific steps. The infrastructure workflow also uses the stable deployment name `txv-infra-dev` to keep output lookup deterministic for the application deployment pipeline.

The repository also assumes the Bicep deployment produces resource outputs including:

- `resourceGroupName`
- `acrName`
- `apiAppName`
- `mockAppName`

Those outputs are resolved with `az deployment sub show --query properties.outputs` and then used to drive the Container App rollout.

## Source Ownership

Primary owners:

- `.github/workflows/ci.yml` — CI validation and Codecov upload
- `.github/workflows/integration.yml` — integration test gate
- `.github/workflows/infra.yml` — Bicep preview and apply
- `.github/workflows/deploy-azure.yml` — Container App deployment and health verification
- `infra/bicep/main.bicep` — Azure resource definitions and deployment outputs
- `codecov.yml` — coverage threshold and exclusions

Supporting owners:

- `.vscode/tasks.json` — local parity for build/test/coverage execution
- `docs/operations/deployment/workflows/github/README.md` — repository pipeline index
- `docs/operations/deployment/azure_deployment/README.md` — operations and identity guidance

## Verification Surface

The following checks prove the delivery model:

- GitHub workflow YAML review for triggers, permissions, concurrency, and environment usage
- Local parity check against `.vscode/tasks.json` for restore/build/test/coverage commands
- CI unit verification in `ci.yml` against `Category!=Integration&Category!=E2E`
- Integration workflow review against `Category=Integration`
- Infra workflow review against Bicep preview/apply responsibilities
- Deployment workflow review against Azure login, output resolution, image build, revision update, and `/healthz` verification
- Codecov status policy in `codecov.yml` and the CI uploader step in `ci.yml`

## Implementation Pattern Rules

### Pattern: Keep infrastructure and application delivery in separate workflows

- **Applies to** - Azure delivery lifecycle.
- **Intent** - prevent accidental infra reapplication during normal app updates.
- **Rule** - the repository MUST keep `infra.yml` focused on Bicep preview/apply and `deploy-azure.yml` focused on application image rollout. App deployment MUST NOT re-run infrastructure provisioning as part of the normal push path.
- **Approved code shape**:

```yaml
# infra.yml
on:
  pull_request:
    paths:
      - 'infra/bicep/**'

# deploy-azure.yml
on:
  push:
    branches: [main]
    paths:
      - 'src/**'
```

- **Avoid** - mixing Bicep changes and runtime image changes in one deployment workflow, or making the app workflow depend on a fresh infra apply on every repo push.
- **Implementation references** - `.github/workflows/infra.yml`, `.github/workflows/deploy-azure.yml`
- **Verification** - workflow inspection and trigger-path review

### Pattern: Use OIDC for Azure access

- **Applies to** - both Azure workflows.
- **Intent** - rely on federated identity rather than long-lived client secrets.
- **Rule** - Azure workflows MUST authenticate with `azure/login@v2` using the configured `client-id`, `tenant-id`, and `subscription-id` secrets. They MUST declare `id-token: write` and `contents: read` permissions.
- **Approved code shape**:

```yaml
permissions:
  id-token: write
  contents: read

- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

- **Avoid** - static credentials, secret-bearing shell commands, or unscoped environment assumptions.
- **Implementation references** - `.github/workflows/deploy-azure.yml`, `.github/workflows/infra.yml`
- **Verification** - workflow YAML review and Azure environment setup docs

### Pattern: Resolve deployment outputs before app rollout

- **Applies to** - application deployment to Azure Container Apps.
- **Intent** - keep infrastructure and runtime names aligned with the current Azure deployment outputs.
- **Rule** - the deploy workflow MUST query `az deployment sub show --query properties.outputs` and then use the resolved `resourceGroupName`, `acrName`, `apiAppName`, and `mockAppName` values for subsequent commands.
- **Approved code shape**:

```bash
outputs=$(az deployment sub show --name "${{ env.INFRA_DEPLOYMENT_NAME }}" --query properties.outputs -o json)

echo "RESOURCE_GROUP=$(echo "$outputs" | jq -r '.resourceGroupName.value')" >> "$GITHUB_ENV"
```

- **Avoid** - hardcoded names that drift from the active Bicep deployment or a deployment that is not created yet.
- **Implementation references** - `.github/workflows/deploy-azure.yml`
- **Verification** - workflow inspection and Azure resource resolution review

### Pattern: Verify deployment health before declaring success

- **Applies to** - Azure app deployment updates.
- **Intent** - ensure the platform is not only updated, but also running in a healthy revision.
- **Rule** - the deploy workflow MUST wait for an active running revision and MUST probe the API `/healthz` endpoint before reporting success.
- **Approved code shape**:

```bash
az containerapp revision list --name "$app" --resource-group "${{ env.RESOURCE_GROUP }}" --query "[?properties.active && properties.runningState=='Running'] | length(@)" -o tsv
curl -sf --retry 5 --retry-delay 10 --retry-all-errors "https://$API_FQDN/healthz"
```

- **Avoid** - assuming image push means the app is live, or skipping the health check after a revision update.
- **Implementation references** - `.github/workflows/deploy-azure.yml`
- **Verification** - workflow step review and deployment health logic

### Pattern: Keep coverage upload separate from workflow success semantics

- **Applies to** - repository quality gates and Codecov integration.
- **Intent** - ensure the upload step cannot silently erase the unit coverage signal without making the workflow brittle.
- **Rule** - the CI workflow MUST upload Cobertura from the unit test run and MUST configure `fail_ci_if_error: false` while preserving repository branch protection as the enforcement boundary. The configured status remains in Codecov rather than directly in the YAML.
- **Approved code shape**:

```yaml
- uses: codecov/codecov-action@v5
  with:
    token: ${{ secrets.CODECOV_TOKEN }}
    files: ./coverage/**/coverage.cobertura.xml
    fail_ci_if_error: false
```

- **Avoid** - claiming the uploader is a runtime gate when enforcement is actually handled by the repository branch protection and Codecov status.
- **Implementation references** - `.github/workflows/ci.yml`, `codecov.yml`
- **Verification** - workflow review and Codecov configuration review

## Operational Boundaries

- The repository does not provide an automated GitHub E2E workflow for Docker-backed end-to-end validation.
- The Azure deployment workflow requires a pre-existing Bicep deployment and live Azure subscription access.
- The infrastructure workflow is a deliberate gate for resource changes and is not used as a normal application deployment trigger.
- The delivery model assumes an existing Azure environment plus the correct GitHub environment approvals and OIDC identity setup.
- The deployment workflow does not cover schema migration, data-plane operations, or non-Container App runtime components beyond the application rollout and health verification.

## Related Supporting Documents

- [GitHub workflow documentation](../../operations/deployment/workflows/github/README.md)
- [Azure deployment documentation](../../operations/deployment/azure_deployment/README.md)
- [Testing and quality feature](../testing-and-quality/README.md)
- [Runtime and API feature](../runtime-and-api/README.md)
