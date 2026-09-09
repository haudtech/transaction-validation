# Deploy to Azure Workflow Guide (`.github/workflows/deploy-azure.yml`)

Purpose: build the API and Mock container images on the runner, push them to Azure Container Registry (ACR), point the existing Container Apps at the new tags, and verify the deployment is healthy. It never touches `infra/bicep` — infrastructure changes are handled by [infra_workflow.md](infra_workflow.md).

## 1) Command line-by-line explanation

### Trigger conditions
Configuration:
```yaml
on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - '.github/workflows/deploy-azure.yml'
  workflow_dispatch:
```
Explanation:
- Runs on pushes to `main`, but only when application code (`src/**`) or the workflow file itself changed. Pure docs/infra changes do not trigger a redeploy.
- `workflow_dispatch` allows manual deploys (e.g., re-deploy after an external fix).

### Concurrency
Configuration:
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```
Explanation:
- Serializes deployments per ref: a newer push cancels an in-progress deployment of the same branch, so back-to-back merges cannot interleave deploys.

### Permissions
Configuration:
```yaml
permissions:
  id-token: write
  contents: read
```
Explanation:
- `id-token: write` is required for OIDC-based Azure login (no stored service principal secret).
- `contents: read` allows checking out the repository.

### Step: Checkout
Configuration:
```yaml
uses: actions/checkout@v5
```
Explanation:
- Checks out repository source code into the runner workspace.

### Step: Azure login (OIDC)
Configuration:
```yaml
uses: azure/login@v2
with:
  client-id: ${{ secrets.AZURE_CLIENT_ID }}
  tenant-id: ${{ secrets.AZURE_TENANT_ID }}
  subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```
Explanation:
- Authenticates to Azure using OpenID Connect federation; credentials come from repository secrets. See [azure_deployment/oidc_prerequisite_setup.md](../../azure_deployment/oidc_prerequisite_setup.md) for setup.

### Step: Resolve resource names from infra deployment outputs
Command:
```bash
outputs=$(az deployment sub show \
  --name "${{ env.INFRA_DEPLOYMENT_NAME }}" \
  --query properties.outputs -o json)
echo "RESOURCE_GROUP=$(echo "$outputs" | jq -r '.resourceGroupName.value')" >> "$GITHUB_ENV"
echo "ACR_NAME=$(echo "$outputs" | jq -r '.acrName.value')" >> "$GITHUB_ENV"
echo "API_APP_NAME=$(echo "$outputs" | jq -r '.apiAppName.value')" >> "$GITHUB_ENV"
echo "MOCK_APP_NAME=$(echo "$outputs" | jq -r '.mockAppName.value')" >> "$GITHUB_ENV"
```
Explanation:
- Reads the outputs of the stable infra deployment (`txv-infra-dev`) instead of hardcoding resource names.
- `az deployment sub show`: fetches a subscription-scoped deployment by name.
- `--query properties.outputs`: extracts just the outputs object.
- Each output is written to `$GITHUB_ENV`, making it an environment variable for later steps.

### Step: Log in to ACR
Command:
```bash
az acr login --name ${{ env.ACR_NAME }}
```
Explanation:
- Authenticates the runner's Docker daemon against the resolved ACR so images can be pushed.

### Step: Build and push API image
Command:
```bash
docker build \
  --file src/TransactionValidation.Api/Dockerfile \
  --tag ${{ env.ACR_NAME }}.azurecr.io/txv-api:${{ github.sha }} \
  .
docker push ${{ env.ACR_NAME }}.azurecr.io/txv-api:${{ github.sha }}
```
Explanation:
- Builds the API image from its Dockerfile with the full repo as build context.
- Tags the image with the commit SHA (`github.sha`) so every deployment is an immutable, traceable tag.
- Pushes the image to ACR.
- Note: `az acr build` (ACR Tasks) is unavailable on this subscription (`TasksOperationsNotAllowed`), hence building on the runner.

### Step: Build and push Mock image
Same pattern as the API image, using `src/TransactionValidation.Mock/Dockerfile` and the `txv-mock` repository name.

### Step: Update API Container App revision
Command:
```bash
az containerapp update \
  --name ${{ env.API_APP_NAME }} \
  --resource-group ${{ env.RESOURCE_GROUP }} \
  --image ${{ env.ACR_NAME }}.azurecr.io/txv-api:${{ github.sha }}
```
Explanation:
- Points the existing API Container App at the newly pushed image tag, which creates and activates a new revision.
- `--image`: only the image changes; all other app configuration is preserved.

### Step: Update Mock Container App revision
Same pattern as the API step, targeting the Mock app.

### Step: Verify deployment health
Command:
```bash
for app in "${{ env.API_APP_NAME }}" "${{ env.MOCK_APP_NAME }}"; do
  for i in $(seq 1 12); do
    running=$(az containerapp revision list \
      --name "$app" \
      --resource-group "${{ env.RESOURCE_GROUP }}" \
      --query "[?properties.active && properties.runningState=='Running'] | length(@)" \
      -o tsv)
    ...
  done
done
API_FQDN=$(az containerapp show ... --query properties.configuration.ingress.fqdn -o tsv)
curl -sf --retry 5 --retry-delay 10 --retry-all-errors "https://$API_FQDN/healthz"
```
Explanation:
- First loop: waits (up to ~2 minutes, 12 × 10 s) until each app has at least one active revision in `Running` state; fails the workflow with `::error::` otherwise.
- Second part: resolves the API app's ingress FQDN and calls the API's health endpoint (`/healthz`, mapped in `Program.cs`) with retries.
- A broken deployment (crash loop, bad config) now fails the workflow on `main` instead of passing silently.

## 2) When it is triggered

Defined triggers in `deploy-azure.yml`:
- `push` to `main` where `src/**` or the workflow file changed
- `workflow_dispatch` (manual)

Additional behavior:
- Job uses `environment: azure-dev`, so repository environment protection rules can gate execution. For the standard auto-deploy-on-merge flow, `azure-dev` is intentionally left without required reviewers.
- Concurrency: per-ref group with `cancel-in-progress: true`.

Practical trigger examples:
- Merge a PR that changed `src/**` into `main` => deploy runs automatically.
- Merge a docs-only PR => deploy does not run.
- Manually click **Run workflow** => deploy runs regardless of path filters.

## 3) How to trigger it manually

### GitHub UI
1. Open repository on GitHub.
2. Go to **Actions**.
3. Select **Deploy to Azure** workflow.
4. Click **Run workflow**.
5. Choose branch/ref and run.

### GitHub CLI
```bash
gh workflow run deploy-azure.yml --ref main
```

### Re-run existing run
From the workflow run page:
- Click **Re-run all jobs** (or re-run failed jobs).
