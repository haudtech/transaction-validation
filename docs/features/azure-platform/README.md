# Azure Platform

## Purpose and Scope

This document covers the implemented Azure runtime model for TransactionValidation. It explains the Bicep-managed resource group, identity and network boundaries, the Service Bus and Redis dependencies, the managed Key Vault and Azure Monitor setup, and the Container Apps deployment model used by the repository.

It does not duplicate workflow command sequences or describe the full app runtime behavior in detail. Those concerns remain in the feature docs for the application runtime, messaging, infrastructure delivery, and Azure operations runbooks.

## Behavior Inventory

- Azure subscription-scoped infrastructure deployment
- Resource group and network topology
- Service Bus namespace and topic/subscription model
- Redis + Key Vault provisioning
- Observability resource provisioning
- Container Apps for API and Mock
- OIDC-based Azure identity in GitHub Actions
- Resource output resolution for deployment automation
- Environment-specific naming and configuration separation

## Technologies and Techniques

| Functionality or behavior | Technology | Technique | Implementation owner | Verification |
|---|---|---|---|---|
| Subscription deployment | Bicep, Azure Resource Manager | Single entry point creates the resource group and all child modules | `infra/bicep/main.bicep` | Bicep review and workflow inspection |
| Network topology | VNet + private endpoints | Dedicated infrastructure and private-access subnets | `infra/bicep/modules/vnet.bicep` | Bicep module review |
| Messaging service | Azure Service Bus | Shared namespace with broker-specific routing and private endpoint connectivity | `infra/bicep/modules/servicebus.bicep` | Bicep module and messaging feature review |
| Distributed state | Azure Cache for Redis | Redis instance with private endpoint and secret injection | `infra/bicep/modules/redis.bicep` | Bicep module and idempotency docs |
| Secret and config distribution | Azure Key Vault | Store API key and runtime connection strings for app access | `infra/bicep/modules/keyvault.bicep` | Bicep review and app configuration contract |
| Observability | Log Analytics + Application Insights | Centralized logs and Azure Monitor export path | `infra/bicep/modules/observability.bicep` | Observability feature and module review |
| Container hosting | Azure Container Apps | API and Mock applications run in separate app revisions | `infra/bicep/modules/containerapps.bicep` | Deployment workflow and app health checks |
| Azure identity | OIDC + RBAC | Federated GitHub identity grants deployment rights without long-lived secrets | GitHub workflows and Entra setup docs | Workflow and OIDC runbook review |

## Definitions and Semantics

### Resource group and environment model

The repository uses a single subscription-targeted Bicep entry point with a stable environment naming pattern:

- `resourceGroupName = rg-${namePrefix}-${environmentName}`
- environment default: `dev`
- name prefix default: `txv`

This keeps resource names deterministic and allows the deployment workflow to read outputs from the same stable deployment name.

### Deployment outputs

The infrastructure entry point exports a small stable contract consumed by the application deployment workflow:

- `resourceGroupName`
- `apiFqdn`
- `mockFqdn`
- `acrLoginServer`
- `acrName`
- `apiAppName`
- `mockAppName`

The application workflow resolves these through `az deployment sub show --query properties.outputs` and then updates existing Container App revisions.

### Broker mode boundary

The local Docker flow defaults to RabbitMQ. In Azure, the application can run with the Azure Service Bus publisher and consumer configuration while still preserving a broker-neutral application contract. The shell and config values are selected through configuration and environment contracts rather than by hard-coded app logic.

### Network and secret boundary

The Azure resource set includes private endpoint support for Service Bus and Redis and centralizes connection strings in Key Vault. The application runtime resolves those values through configuration and secure secret references rather than committing live values to source control.

## Configuration Contract

The infrastructure contract is defined in `infra/bicep/main.bicep` and its child modules.

Required input values:

- `location` (default: `eastus`)
- `environmentName` (default: `dev`)
- `namePrefix` (default: `txv`)
- `apiKeySecretValue` (secure secret; passed at deploy time)

Derived output values used by deployment automation:

- `resourceGroupName`
- `acrName`
- `apiAppName`
- `mockAppName`

The Bicep module chain is responsible for generating names that match the Azure resources created by the repository. The application deployment workflow resolves these outputs rather than relying on hand-maintained names.

## Source Ownership

Primary owners:

- `infra/bicep/main.bicep` — subscription-scoped Azure resource orchestration
- `infra/bicep/modules/*.bicep` — composed resource modules
- `.github/workflows/infra.yml` — infra preview/apply flow
- `.github/workflows/deploy-azure.yml` — deploy application revision after infra exists
- `docs/operations/deployment/azure_deployment/` — operational identity and validation runbooks

Supporting owners:

- `../infrastructure-and-delivery/README.md` — repo delivery boundary and workflow responsibilities
- `../observability/README.md` — platform telemetry and health signal design
- `../messaging/README.md` — broker semantics and Azure Service Bus adapter model

## Verification Surface

The Azure platform model is proven by the following sources:

- Bicep resource definitions in `infra/bicep/main.bicep`
- module outputs used by deployment automation
- Azure workflow review for OIDC, output resolution, and Container App revision management
- validation and shutdown runbook for live environment checks
- local and Azure-specific configuration references in the app and environment templates

## Implementation Pattern Rules

### Pattern: keep infra and app delivery distinct

- **Applies to** - Azure resource lifecycle and deployment automation.
- **Intent** - avoid accidental drift between resource creation and application rollout.
- **Rule** - the repository MUST separate resource provisioning from application image deployment. Infrastructure previews and applies MUST be performed by the Bicep workflow; container image updates MUST happen in the application deployment workflow.
- **Approved code shape**:

```yaml
# infra workflow
az deployment sub what-if
az deployment sub create

# deploy workflow
az containerapp update --image <acr>/txv-api:<tag>
```

- **Avoid** - moving production resource creation into the app deploy workflow, or treating the app workflow as the authoritative creation path.
- **Implementation references** - `infra.yml`, `deploy-azure.yml`, `main.bicep`
- **Verification** - workflow review and deployment boundary documentation

### Pattern: resolve Azure outputs before app rollout

- **Applies to** - application deployment to Container Apps.
- **Intent** - keep Azure app runtime configuration synced with the live provisioned environment.
- **Rule** - the deployment workflow MUST query the stable infra deployment outputs before updating the Container App resources. It MUST not assume persisted names or stale hardcoded values.
- **Approved code shape**:

```bash
outputs=$(az deployment sub show --name "${{ env.INFRA_DEPLOYMENT_NAME }}" --query properties.outputs -o json)
```

- **Avoid** - hardcoded app names or output assumptions that drift from the current `main.bicep` resource names.
- **Implementation references** - `deploy-azure.yml`, `main.bicep`
- **Verification** - workflow and output review

### Pattern: gate deploys by health as well as revision activation

- **Applies to** - Azure Container Apps rollout.
- **Intent** - confirm the revision is live and the app is actually serving health checks.
- **Rule** - the workflow MUST wait for a running revision and probe `/healthz` before claiming deployment success.
- **Approved code shape**:

```bash
curl -sf --retry 5 --retry-delay 10 --retry-all-errors "https://$API_FQDN/healthz"
```

- **Avoid** - considering image push or app update success without verifying a live HTTP readiness signal.
- **Implementation references** - `deploy-azure.yml`, `Program.cs`
- **Verification** - workflow review and health endpoint contract

## Operational Boundaries

- Azure resources are provisioned declaratively through Bicep; the repository should not rely on ad hoc manual resource creation for the standard support path.
- The app deployment workflow assumes the Azure environment already exists and is valid.
- Key Vault, API keys, and other secrets are not committed to source control; secret handling is an operational concern documented in the Azure OIDC and deployment runbooks.
- The runtime model assumes environment-specific naming and identity are managed as part of the repository's Azure deployment flow.
