# Azure CLI Command Guide

This guide is a command-by-command reference for the TransactionValidation Azure `dev` environment. It collects the commands used to authenticate, provision supporting resources, deploy the Bicep infrastructure, validate Container Apps, inspect failures, and shut the environment down.

The repository's infrastructure workflow is the normal provisioning path. The manual Service Bus commands in this guide are useful for recovery, troubleshooting, or recreating messaging resources outside the Bicep deployment.

## Safety and shell conventions

- Run commands only after confirming the target subscription and resource group.
- Replace placeholders such as `<AZURE_SUBSCRIPTION_ID>` before running a command.
- Never commit or paste API keys, SAS connection strings, Key Vault secret values, or other credentials into documentation, source, or issue comments.
- Use `read -r -s` for secrets so they are not echoed. Avoid commands that print secret values.
- Run from the repository root when a command refers to `infra/bicep/main.bicep`.
- In a multi-line shell command, the backslash must be the final character on the line. Do not add spaces after `\`.
- Azure CLI command names vary by CLI version. Use the help command shown in the troubleshooting section when an operation is reported as unrecognized.

Example environment for the current default deployment:

```bash
SUBSCRIPTION_ID=<AZURE_SUBSCRIPTION_ID>
LOCATION=eastus
RESOURCE_GROUP=rg-txv-dev
DEPLOYMENT_NAME=txv-infra-dev
API_APP=txv-api-dev
MOCK_APP=txv-mock-dev
SERVICE_BUS_NAMESPACE=sb-txv-dev-001
```

## 1. Authenticate and select the subscription

### `az login`

```bash
az login
```

Opens the browser-based Azure login flow and lists the tenants and subscriptions available to the signed-in identity. Select the subscription that owns the TransactionValidation environment.

For a terminal without a usable browser:

```bash
az login --use-device-code
```

Guidance: login is local operator authentication. GitHub Actions uses OIDC through `azure/login@v2` and does not use this interactive command.

### `az account list`

```bash
az account list -o table
```

Lists subscriptions available to the current Azure CLI session. Check the subscription ID, tenant, state, and default marker before provisioning resources.

To list only subscription IDs:

```bash
az account list --query "[].id" -o tsv
```

### `az account set`

```bash
az account set --subscription "$SUBSCRIPTION_ID"
```

Changes the active subscription for subsequent Azure CLI commands. Always run this after login when more than one subscription is available.

### `az account show`

```bash
az account show -o table
```

Confirms the active subscription and tenant. Use JSON when a script needs the full context:

```bash
az account show -o json
```

Expected state is the intended subscription with `state` set to `Enabled`. `az account list` shows all accessible subscriptions; it does not prove which one is active, so use `az account show` for that check.

## 2. Inspect or create the resource group

### `az group list`

```bash
az group list -o table
az group list --query "[].name" -o tsv
```

Lists resource groups visible in the active subscription. An empty result means no resource groups are visible in that subscription; verify the subscription before creating resources.

### `az group create`

```bash
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"
```

Creates a resource group. The subscription-scoped Bicep deployment also creates or updates this group, so manual creation is normally unnecessary. Use this command only for a deliberate bootstrap or recovery operation.

Guidance: the resource group name must match the Bicep naming convention (`rg-<prefix>-<environment>`). Do not create a second similarly named group by accident.

### `az group show`

```bash
az group show \
  --name "$RESOURCE_GROUP" \
  --query "{name:name,location:location,provisioningState:properties.provisioningState,tags:tags}" \
  -o table
```

Confirms that the resource group exists and has completed provisioning.

## 3. Create Service Bus resources manually

The Bicep deployment is authoritative. Use these commands only when manually rebuilding or repairing the messaging topology.

### `az servicebus namespace create`

```bash
az servicebus namespace create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SERVICE_BUS_NAMESPACE" \
  --location "$LOCATION" \
  --sku Standard
```

Creates a Service Bus namespace. Azure may automatically register the `Microsoft.ServiceBus` resource provider on the first use; the caller must have permission to register providers.

The repository's Bicep configuration may use a different SKU or networking configuration. Match the infrastructure code when manually recovering an environment rather than assuming this example is equivalent.

If Azure reports that the resource provider is not registered, register it explicitly and wait for completion:

```bash
az provider register \
  --namespace Microsoft.ServiceBus \
  --wait
```

`az provider register` changes subscription provider state and may require additional subscription permissions.

### `az servicebus topic create`

```bash
az servicebus topic create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --name partner.transactions
```

Creates the transaction topic in the namespace.

### `az servicebus topic subscription create`

Primary subscription:

```bash
az servicebus topic subscription create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --topic-name partner.transactions \
  --name partner-transactions
```

Audit subscription:

```bash
az servicebus topic subscription create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --topic-name partner.transactions \
  --name partner-transactions.audit
```

Creates independent subscriptions so the primary and audit consumers each receive the messages selected by their rules.

### `az servicebus topic subscription rule create`

Primary rule:

```bash
az servicebus topic subscription rule create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --topic-name partner.transactions \
  --subscription-name partner-transactions \
  --name primary-match \
  --filter-type SqlFilter \
  --filter-sql-expression "eventType IN ('partner.transaction.accepted','partner.transaction.rejected','partner.transaction.pending')"
```

Audit rule:

```bash
az servicebus topic subscription rule create \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --topic-name partner.transactions \
  --subscription-name partner-transactions.audit \
  --name audit-match \
  --filter-type SqlFilter \
  --filter-sql-expression "eventType = 'partner.transaction.accepted'"
```

`--filter-type SqlFilter` declares the rule filter type. `--filter-sql-expression` supplies the Service Bus SQL expression.

Important: this Azure CLI version does not accept `--sql-filter`. The failed form:

```bash
az servicebus topic subscription rule create ... --sql-filter "..."
```

must be replaced with `--filter-type SqlFilter --filter-sql-expression "..."`.

Inspect the supported arguments on the current machine:

```bash
az servicebus topic subscription rule create -h
```

### `az upgrade`

```bash
az upgrade
```

Updates the Azure CLI installation when a newer version is available. Perform this as a local maintenance operation and rerun `-h` after upgrading because command arguments can change between CLI versions.

### `az extension add`

```bash
az extension add --name servicebus --upgrade
```

This was attempted during troubleshooting, but the installed Azure CLI reported that no extension named `servicebus` exists. The Service Bus commands used here are built into the installed CLI, so do not install an extension unless the current CLI help or official Azure documentation explicitly requires one.

### `az servicebus namespace show`

```bash
az servicebus namespace show \
  --name "$SERVICE_BUS_NAMESPACE" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name,status:status,provisioningState:provisioningState,sku:sku.name,endpoint:serviceBusEndpoint}" \
  -o table
```

Checks namespace status and SKU without retrieving credentials.

### `az servicebus namespace authorization-rule keys list`

```bash
az servicebus namespace authorization-rule keys list \
  --resource-group "$RESOURCE_GROUP" \
  --namespace-name "$SERVICE_BUS_NAMESPACE" \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv
```

Retrieves a SAS connection string. This is a sensitive operation and the output must not be pasted into logs, documentation, chat, or shell history. Prefer managed identity and Key Vault for application access. Use this only for a controlled diagnostic or legacy client that explicitly requires SAS.

## 4. Preview and apply Bicep infrastructure

The GitHub `infra.yml` workflow is the preferred path. It runs a subscription-scoped deployment using `infra/bicep/main.bicep`, the dev parameters file, and the API key supplied from a GitHub secret.

### `az deployment sub what-if`

```bash
az deployment sub what-if \
  --location "$LOCATION" \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.dev.json \
  --parameters apiKeySecretValue="$API_KEY_SECRET_VALUE"
```

Previews resource changes without applying them. Use this for a PR or before a deliberate manual deployment.

Guidance: set `API_KEY_SECRET_VALUE` without printing it, or pass it from a secure shell mechanism. Do not replace the parameter with a real value in the committed JSON file.

### `az deployment sub create`

```bash
az deployment sub create \
  --name "$DEPLOYMENT_NAME" \
  --location "$LOCATION" \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.dev.json \
  --parameters apiKeySecretValue="$API_KEY_SECRET_VALUE"
```

Creates or updates the subscription-scoped deployment. The deployment creates the resource group and invokes the resource-group-scoped modules for networking, Service Bus, Redis, Key Vault, Container Apps, and observability.

Guidance:

- Use a stable deployment name so later commands can read its outputs.
- Keep the deployment name aligned with `INFRA_DEPLOYMENT_NAME` in the workflows.
- A successful parent deployment can still contain failed nested operations; inspect operations when the command returns `DeploymentFailed`.

### `az deployment sub show`

```bash
az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "{name:name,state:properties.provisioningState,location:location}" \
  -o table
```

Shows the parent deployment state.

Read non-secret outputs:

```bash
az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query properties.outputs \
  -o json
```

Resolve the API host from the deployment output:

```bash
API_HOST=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query properties.outputs.apiFqdn.value \
  -o tsv)
API_URL="https://${API_HOST}"
echo "API: ${API_URL}"
```

Do not print secure outputs or deployment parameters.

## 5. Diagnose failed deployments

### `az deployment operation sub list`

```bash
az deployment operation sub list \
  --name "$DEPLOYMENT_NAME" \
  --query "[].{state:properties.provisioningState,code:properties.statusMessage.error.code,message:properties.statusMessage.error.message,target:properties.targetResource.resourceName}" \
  -o json
```

Lists top-level operations for a subscription deployment. The current Azure CLI syntax is `az deployment operation sub list`; `az deployment sub operation list` is not recognized by the CLI used for this environment.

### `az deployment operation group list`

```bash
NESTED_DEPLOYMENT_NAME=<FAILED_RESOURCE_GROUP_DEPLOYMENT_NAME>

az deployment operation group list \
  --resource-group "$RESOURCE_GROUP" \
  --name "$NESTED_DEPLOYMENT_NAME" \
  --query "[].{state:properties.provisioningState,code:properties.statusMessage.error.code,message:properties.statusMessage.error.message,target:properties.targetResource.resourceName}" \
  -o json
```

Inspects the resource-group-scoped module deployment named by the failed parent operation. This is where the actionable resource error usually appears.

Example diagnosis flow:

1. `az deployment sub create` reports `DeploymentFailed`.
2. `az deployment operation sub list` identifies a failed nested deployment such as `observability-<unique-string>`.
3. `az deployment operation group list` reveals the failing resource and its service-specific error.

For the previous observability failure, this command exposed that `RetentionInDays: 5` violated the Log Analytics SKU limits. Azure Monitor retention was changed to the supported 30-day minimum.

## 6. Inspect Container Apps

### `az containerapp show`

```bash
for app in "$API_APP" "$MOCK_APP"; do
  az containerapp show \
    --name "$app" \
    --resource-group "$RESOURCE_GROUP" \
    --query "{name:name,provisioningState:properties.provisioningState,runningStatus:properties.runningStatus,latestRevision:properties.latestRevisionName}" \
    -o table
done
```

Confirms that the API and Mock Container Apps are provisioned and running.

### `az containerapp revision list`

```bash
az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name,active:properties.active,health:properties.healthState,replicas:properties.replicas,provisioning:properties.provisioningState}" \
  -o table
```

Lists revisions and their active, health, replica, and provisioning state. Use `-o table` without a query if an older CLI version rejects a query field:

```bash
az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  -o table
```

Find the active revision for scripted operations:

```bash
API_REVISION=$(az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?properties.active].name | [0]" \
  -o tsv)
```

### `az containerapp revision show`

```bash
az containerapp revision show \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --revision "$API_REVISION" \
  --query "{name:name,active:properties.active,health:properties.healthState}" \
  -o table
```

Verifies the state of one known revision. Repeat with `MOCK_APP` and `MOCK_REVISION` for the Mock service.

### `az containerapp logs show`

```bash
az containerapp logs show \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --follow
```

Streams API container logs. For Mock consumer and Service Bus processing:

```bash
az containerapp logs show \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --follow
```

Press `Ctrl+C` to stop following logs. The command does not stop the Container App.

### `az resource list`

```bash
az resource list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name,type:type,state:properties.provisioningState}" \
  -o table
```

Provides a broad inventory of resources and is useful when confirming what a deployment created or when investigating a partially failed deployment.

## 7. Validate the deployed API

### `curl` health check

```bash
curl --fail-with-body --silent --show-error \
  "${API_URL}/healthz"
printf '\\n'
```

Checks the public health endpoint and fails the shell command for an HTTP error. A successful response is expected to be `Healthy` with HTTP 200.

### Read an API key without echoing it

```bash
read -r -s -p 'API key: ' API_KEY
printf '\\n'
export API_KEY
```

Loads the API key into the current shell without displaying it. The API key is stored in Key Vault as `Security--ApiKey`; retrieve it only through an approved operator workflow.

### Authenticated API request

```bash
curl --silent --show-error \
  --write-out '\\nHTTP %{http_code}\\n' \
  -H "X-API-Key: ${API_KEY}" \
  "${API_URL}/"
```

Checks API-key authentication. Keep response output free of secret headers and do not use shell tracing (`set -x`) while `API_KEY` is set.

For a transaction smoke test, use the existing [validation and shutdown runbook](validation_and_shutdown_runbook.md), which includes the request body, idempotency key, and expected `202` response.

## 8. Deactivate and reactivate Container App revisions

There is no supported `az containerapp stop` command in the CLI version used for this environment. The failed form:

```bash
az containerapp stop --name "$API_APP" --resource-group "$RESOURCE_GROUP"
```

is not a valid replacement for revision lifecycle management.

### `az containerapp revision deactivate`

```bash
API_REVISION=$(az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?properties.active].name | [0]" \
  -o tsv)

MOCK_REVISION=$(az containerapp revision list \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?properties.active].name | [0]" \
  -o tsv)

test -n "$API_REVISION" || { echo "No active API revision found" >&2; exit 1; }
test -n "$MOCK_REVISION" || { echo "No active Mock revision found" >&2; exit 1; }

az containerapp revision deactivate \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --revision "$API_REVISION"

az containerapp revision deactivate \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --revision "$MOCK_REVISION"
```

Deactivates the active revisions after explicitly checking that revision names were found. This removes application traffic but does not delete the infrastructure or stop charges for managed resources.

### `az containerapp revision activate`

```bash
az containerapp revision activate \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --revision "$API_REVISION"

az containerapp revision activate \
  --name "$MOCK_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --revision "$MOCK_REVISION"
```

Reactivates known revisions. Verify health afterward with `az containerapp revision show` and `az containerapp show`.

### `az containerapp start` and `az containerapp stop`

These commands may appear in generic Container Apps documentation, but `start`/`stop` were not available in the CLI version used for this environment. Use revision activation/deactivation and confirm the locally installed command surface with:

```bash
az containerapp -h
az containerapp revision -h
```

## 9. Trigger or inspect GitHub workflows

These commands require GitHub CLI (`gh`) and an authenticated GitHub session.

### `gh workflow run`

```bash
gh workflow run infra.yml --ref main
gh workflow run deploy-azure.yml --ref main
```

Manually starts the infrastructure or application deployment workflow. `infra.yml` applies Bicep and is environment-gated; `deploy-azure.yml` builds and deploys application images.

### `gh workflow disable` and `gh workflow enable`

```bash
gh workflow disable infra.yml
gh workflow disable deploy-azure.yml
```

Temporarily disables automated workflow triggers. Re-enable them with:

```bash
gh workflow enable infra.yml
gh workflow enable deploy-azure.yml
```

Use this only as a deliberate maintenance measure. Disabling workflows does not stop already-running Azure resources.

## 10. Delete the environment

### `az group delete`

```bash
az group show \
  --name "$RESOURCE_GROUP" \
  --query "{name:name,location:location,tags:tags}" \
  -o json

az group delete \
  --name "$RESOURCE_GROUP" \
  --yes \
  --no-wait
```

Deletes the complete resource group asynchronously. This is destructive and removes application resources, networking, Service Bus, Redis, Key Vault, ACR, observability resources, and related data in the group.

Use only after verifying the subscription and group name. Prefer omitting `--no-wait` when an operator needs the command to remain attached until Azure confirms completion.

### `az group exists`

```bash
az group exists --name "$RESOURCE_GROUP"
```

Returns `true` while the group exists and `false` after deletion completes.

## 11. Local Docker cleanup

These commands affect only local development services and do not change Azure resources.

```bash
docker compose down
docker compose down -v
```

`docker compose down` stops and removes local containers. `docker compose down -v` also removes local volumes and test data; use it only when that data can be discarded.

## 12. Common command failures

| Symptom | Cause | Corrective action |
|---|---|---|
| `Resource provider 'Microsoft.ServiceBus' ... is not registered` | The provider has not been registered in the subscription. | Allow the first authorized Service Bus operation to register it, or register it explicitly with `az provider register --namespace Microsoft.ServiceBus` and wait for registration. |
| `unrecognized arguments: --sql-filter` | The installed Azure CLI uses the current Service Bus rule option names. | Use `--filter-type SqlFilter --filter-sql-expression "..."`. |
| `'stop' is misspelled or not recognized` for `az containerapp stop` | The installed CLI does not expose a Container Apps stop command. | Deactivate the active revision with `az containerapp revision deactivate`; activate it later with `revision activate`. |
| `DeploymentFailed` with no useful detail | The parent deployment wraps a failed nested module. | Run `az deployment operation sub list`, then `az deployment operation group list` for the nested deployment name. |
| `az deployment sub operation list` is unrecognized | The operation command group is ordered incorrectly. | Use `az deployment operation sub list`. |
| `az group list` returns no groups | The CLI is pointed at a different subscription or tenant. | Run `az account show`, then `az account set --subscription "$SUBSCRIPTION_ID"`. |
| `/Users/.../.zprofile: source ... .zshrc: no such file` | A local shell startup file references a missing file. | Repair the shell profile separately; it does not indicate an Azure deployment failure. |
| `/bin/date: option requires an argument -- r` | macOS BSD `date` does not support the GNU `date -r` form used by some scripts. | Use macOS-compatible forms such as `date -u +%Y%m%dT%H%M%SZ` and `date -u +%s`. |
| A multi-line command behaves unexpectedly | A line-continuation backslash has trailing spaces or the command was pasted with literal `\\n`. | Retype the command and ensure `\\` is the final character before the newline. |

## Related documentation

- [Azure deployment README](README.md)
- [Validation and shutdown runbook](validation_and_shutdown_runbook.md)
- [OIDC prerequisite setup](oidc_prerequisite_setup.md)
- [Infrastructure workflow guide](../workflow_actions/github/infra_workflow.md)
- [Application deployment workflow guide](../workflow_actions/github/deploy_azure_workflow.md)
- [Azure deployment plan](../implementation/azure_deployment_plan.md)
