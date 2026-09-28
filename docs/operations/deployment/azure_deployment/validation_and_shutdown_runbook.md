# Azure Deployment Validation and Shutdown Runbook

This runbook validates the deployed `dev` environment and explains how to stop it safely. It assumes the Azure resources created by `infra.yml` are in `eastus` with the default names from `infra/bicep/main.bicep`.

The commands below use the deployment outputs instead of hardcoding the API hostname. Run them from a shell with Azure CLI installed and authenticated.

Replace the placeholders in angle brackets before running a command:

- `<AZURE_SUBSCRIPTION_ID>` — target Azure subscription GUID.
- `<AZURE_RESOURCE_GROUP>` — deployed resource-group name.
- `<AZURE_DEPLOYMENT_NAME>` — subscription deployment name.
- `<AZURE_API_APP_NAME>` and `<AZURE_MOCK_APP_NAME>` — Container App names.
- `<AZURE_SERVICE_BUS_NAMESPACE>` — deployed Service Bus namespace name.

The `API_KEY` variable is intentionally read at runtime and is never included in this document.

## 1. Set the deployment context

```bash
az login
az account set --subscription <AZURE_SUBSCRIPTION_ID>

RESOURCE_GROUP=<AZURE_RESOURCE_GROUP>
DEPLOYMENT_NAME=<AZURE_DEPLOYMENT_NAME>
API_APP=<AZURE_API_APP_NAME>
MOCK_APP=<AZURE_MOCK_APP_NAME>

API_HOST=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query properties.outputs.apiFqdn.value \
  -o tsv)
API_URL="https://${API_HOST}"

echo "API: ${API_URL}"
```

Expected result: the deployed API hostname is printed. Do not expose API keys or secret values in shell history, logs, or chat.

To inspect all deployment outputs without revealing secrets:

```bash
az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query properties.outputs \
  -o json
```

## 2. Validate Azure resource and app state

Confirm the resource group exists:

```bash
az group show \
  --name "$RESOURCE_GROUP" \
  --query "{name:name, location:location, provisioningState:properties.provisioningState}" \
  -o table
```

Check both Container Apps:

```bash
for app in "$API_APP" "$MOCK_APP"; do
  az containerapp show \
    --name "$app" \
    --resource-group "$RESOURCE_GROUP" \
    --query "{name:name, provisioningState:properties.provisioningState, runningStatus:properties.runningStatus, latestRevision:properties.latestRevisionName}" \
    -o table
done
```

Expected result: both apps have successful provisioning and a running status. Check revisions when investigating a failed deployment:

```bash
az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name, active:properties.active, health:properties.healthState, replicas:properties.replicas, provisioning:properties.provisioningState}" \
  -o table
```

If the query reports an error because of an Azure CLI version difference, use this simpler form:

```bash
az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  -o table
```

## 3. Validate API health

The `/healthz` endpoint is intentionally exempt from API-key middleware so Azure Container Apps probes can call it directly:

```bash
curl --fail-with-body --silent --show-error \
  "${API_URL}/healthz"
printf '\n'
```

Expected result: HTTP `200` and a healthy response. This confirms the API process is running, the messaging health check resolves, and the Redis health check can reach the private endpoint.

Check the public root endpoint with the API key when needed:

```bash
curl --silent --show-error \
  --write-out '\nHTTP %{http_code}\n' \
  -H "X-API-Key: ${API_KEY}" \
  "${API_URL}/"
```

A missing or invalid key should return `401`. The API key is stored in Key Vault as `Security--ApiKey`. Only retrieve it when your operator identity has the required access, and keep it in an environment variable rather than printing it:

```bash
read -r -s -p 'API key: ' API_KEY
printf '\n'
export API_KEY
```

## 4. Validate an accepted transaction and idempotency

The following request exercises API key authentication, validation, partner verification, Redis-backed idempotency, and the Service Bus publish path. Use a unique idempotency key for each first request.

```bash
IDEMPOTENCY_KEY="azure-smoke-$(date -u +%Y%m%dT%H%M%SZ)"
TRANSACTION_REFERENCE="azure-smoke-$(date -u +%s)"
TRANSACTION_TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

curl --silent --show-error \
  --write-out '\nHTTP %{http_code}\n' \
  -X POST "${API_URL}/api/v1/partner/transactions" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H "X-API-Key: ${API_KEY}" \
  -H "Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -d @- <<EOF
{
  "partnerId": "azure-smoke-partner",
  "transactionReference": "${TRANSACTION_REFERENCE}",
  "amount": 120.50,
  "currency": "USD",
  "timestamp": "${TRANSACTION_TIMESTAMP}"
}
EOF
```

Expected result: HTTP `202` and a response containing a `messageId`.

Repeat the same request with the same `Idempotency-Key` and payload. The response should demonstrate the configured duplicate/replay behavior. Send the same key with a different payload to confirm the conflict response (`409`).

For a repeatable test sequence, use the existing request examples in [transaction_validation_api_manual.http](../../tests/TransactionValidation.Tests/Integration/http/transaction_validation_api_manual.http). Change its `apiHost` to the value of `API_URL` and provide the API key through the editor's environment support rather than committing it to the file.

## 5. Inspect runtime logs and messaging outcomes

Follow API logs while making a smoke request:

```bash
az containerapp logs show \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --follow
```

Inspect Mock logs separately:

```bash
az containerapp logs show \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --follow
```

The Mock app has internal-only ingress by design. Its HTTP observation/test-support endpoints are not reachable from a normal developer workstation. Therefore:

- validate the public API from the workstation;
- validate Mock consumer processing through logs and Service Bus observations;
- run the repository E2E suite only from a runner with network access to the VNet, or temporarily change the Mock ingress as a deliberate test-only operation.

For a deployment-level check, inspect the Container Apps environment and Service Bus resources:

```bash
az resource list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name, type:type, state:properties.provisioningState}" \
  -o table

az servicebus namespace show \
  --name <AZURE_SERVICE_BUS_NAMESPACE> \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, status:status, sku:sku.name}" \
  -o table
```

## 6. Stop the running services without deleting infrastructure

This is the reversible option. It stops the API and Mock Container Apps while retaining the resource group, networking, Service Bus, Redis, Key Vault, and ACR.

Stopping the apps does not remove the infrastructure or eliminate all Azure charges. Use the resource-group deletion procedure below when the dev environment must be fully retired.

```bash
az containerapp stop \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP"

az containerapp stop \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP"
```

Verify they are stopped:

```bash
for app in "$API_APP" "$MOCK_APP"; do
  az containerapp show \
    --name "$app" \
    --resource-group "$RESOURCE_GROUP" \
    --query "{name:name, runningStatus:properties.runningStatus}" \
    -o table
done
```

Start them again with:

```bash
az containerapp start \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP"

az containerapp start \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP"
```

Stopping the apps does not stop GitHub Actions from deploying new revisions. Avoid pushes that match the workflow paths, or disable the workflows temporarily:

```bash
gh workflow disable infra.yml
gh workflow disable deploy-azure.yml
```

Re-enable them when deployment automation is needed:

```bash
gh workflow enable infra.yml
gh workflow enable deploy-azure.yml
```

## 7. Choose the lowest-cost pause option

Azure does not provide a single pause or suspend operation for every resource in a subscription or resource group. Deactivating the Container App revisions stops application traffic, but it does not stop billing for managed resources that remain provisioned.

For this deployment, the following resources can continue to incur charges after the API and Mock revisions are deactivated:

- Premium Azure Service Bus namespace
- Azure Cache for Redis
- Private endpoints and related networking
- Azure Container Registry storage
- Log Analytics workspace storage and ingestion
- Any other provisioned resource with a paid tier

Use the following choices:

| Goal | Action | Trade-off |
|---|---|---|
| Brief pause with fast recovery | Deactivate the API and Mock revisions in Section 6 | Keeps the infrastructure and its ongoing charges |
| Reduce some compute cost while retaining infrastructure | Deactivate revisions and review each resource's SKU/scale settings | Requires resource-specific changes; data-plane services may still charge |
| Lowest cost for a disposable dev environment | Delete the complete resource group in Section 8 | Destructive: removes resources, data, private endpoints, identities, and ACR images |

There is no resource-group-level command that pauses all billing. Stopping or disabling the subscription is not an appropriate operational substitute because it affects unrelated resources and does not provide a normal application lifecycle.

## 8. Delete the complete dev environment

Use this only when the entire dev environment should be removed. This deletes the Container Apps, private endpoints, VNet, Service Bus, Redis, Key Vault, ACR, Log Analytics, managed identities, and role assignments in the resource group. It is destructive and cannot be undone.

First confirm the target:

```bash
az group show \
  --name "$RESOURCE_GROUP" \
  --query "{name:name, location:location, tags:tags}" \
  -o json
```

Then delete it:

```bash
az group delete \
  --name "$RESOURCE_GROUP" \
  --yes \
  --no-wait
```

Track deletion:

```bash
az group exists --name "$RESOURCE_GROUP"
```

The command returns `false` after deletion completes. Do not run the infrastructure workflow again unless you intend to recreate the environment and still have the required GitHub secrets and Azure RBAC permissions.

## 9. Local Docker shutdown is separate

The Azure commands above do not stop local Docker services. To stop the local RabbitMQ/Redis test stack from the repository root:

```bash
docker compose down
```

Use `docker compose down -v` only when you also want to remove local volumes and their test data.