# GitHub Actions to Azure OIDC Workflow Diagrams

This companion to [oidc_prerequisite_setup.md](oidc_prerequisite_setup.md) models how GitHub Actions authenticates to Azure through OpenID Connect (OIDC), then deploys infrastructure and application images without a stored Azure client secret.

## 1. Actors and responsibilities

| Actor | Responsibility |
|---|---|
| Developer | Creates and reviews changes; approves protected infrastructure deployments |
| Azure CLI | Creates Entra application objects, federated credentials, and Azure RBAC role assignments during one-time setup |
| GitHub repository | Stores workflow files and repository secrets; emits workflow events |
| GitHub Environment | Applies deployment protection rules and identifies the intended deployment context (`azure-infra` or `azure-dev`) |
| GitHub Actions runner | Executes the selected workflow and requests a short-lived OIDC token |
| GitHub OIDC provider | Signs a token identifying the workflow repository and GitHub Environment |
| Microsoft Entra ID | Validates GitHub's token against the App Registration's federated credential and issues an Azure access token |
| App Registration | Defines the non-human application identity and trusted GitHub OIDC subjects |
| Service principal | Tenant-local representation of the App Registration; receives Azure RBAC permissions |
| Azure Resource Manager | Authorizes deployment operations using the access token and service principal's RBAC role |
| Azure Container Registry | Builds/stores images and authorizes Container Apps to pull them |
| Azure Container Apps | Runs API/Mock containers using managed identities to access Azure resources |

## 2. One-time setup: identity and trust

This setup was performed with the commands in [oidc_prerequisite_setup.md](oidc_prerequisite_setup.md).

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Developer
    participant CLI as Azure CLI
    participant Entra as Microsoft Entra ID
    participant Azure as Azure subscription
    participant GitHub as GitHub repository

    Dev->>CLI: az account show
    CLI-->>Dev: Confirm tenant and subscription

    Dev->>CLI: az ad app create
    CLI->>Entra: Create App Registration
    Entra-->>CLI: appId (AZURE_CLIENT_ID)

    Dev->>CLI: az ad sp create --id appId
    CLI->>Entra: Create service principal
    Entra-->>CLI: Service principal object ID

    Dev->>CLI: az role assignment create --role Contributor
    CLI->>Azure: Grant subscription deployment permission
    Azure-->>CLI: Contributor assignment created
    Dev->>CLI: az role assignment create --role User Access Administrator
    CLI->>Azure: Grant resource-group role-assignment permission
    Azure-->>CLI: User Access Administrator assignment created

    Dev->>CLI: az ad app federated-credential create
    CLI->>Entra: Trust numeric azure-infra environment subject
    CLI->>Entra: Trust numeric azure-dev environment subject
    CLI->>Entra: Trust numeric pull_request subject for what-if
    Entra-->>CLI: Federated credentials created

    Dev->>GitHub: Create azure-infra and azure-dev environments
    Dev->>GitHub: Add required reviewer for azure-infra
    Dev->>GitHub: Add AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID
    Dev->>GitHub: Add API_KEY_SECRET_VALUE
```

### Output of setup

```mermaid
flowchart LR
    App["App Registration<br/>txv-github-actions-oidc"]
    App --> FederationInfra["Federated credential<br/>repo:haudtech@110943608/transaction-validation@1333026803<br/>environment:azure-infra"]
    App --> FederationDev["Federated credential<br/>repo:haudtech@110943608/transaction-validation@1333026803<br/>environment:azure-dev"]
    App --> FederationPreview["Federated credential<br/>repo:haudtech@110943608/transaction-validation@1333026803:pull_request"]
    App --> SP["Service principal<br/>tenant-local identity"]
    SP --> RBAC["Contributor<br/>subscription<br/>User Access Administrator<br/>rg-txv-dev"]
    GitHub["GitHub repository"] --> Environments["azure-infra + azure-dev<br/>deployment environments"]
    Environments --> FederationInfra
    Environments --> FederationDev
```

## 3. Infrastructure workflow: preview then apply

[infra.yml](../../.github/workflows/infra.yml) manages Bicep changes. It is intentionally separate from the application-image deployment workflow because network, identity, and data-plane infrastructure changes have a larger blast radius.

```mermaid
flowchart TB
    PR["Pull request changes infra/bicep/**"] --> WhatIf["GitHub Actions: infra.yml what-if job"]
    WhatIf --> LoginPreview["azure/login@v2 requests GitHub OIDC token"]
    LoginPreview --> EntraPreview["Entra validates pull_request federated credential"]
    EntraPreview --> Preview["az deployment sub what-if"]
    Preview --> PRResult["Preview changes only<br/>No Azure resource changes"]

    Merge["Merge infra/bicep/** to main"] --> Gate["azure-infra GitHub Environment"]
    Gate --> Approval{"Required reviewer approves?"}
    Approval -->|No| Stop["Apply job waits / does not deploy"]
    Approval -->|Yes| LoginApply["azure/login@v2 obtains short-lived Azure token"]
    LoginApply --> Deploy["az deployment sub create"]
    Deploy --> Resources["Create/update resource group, VNet, Service Bus,<br/>Redis, Key Vault, ACR, Container Apps and RBAC"]
```

## 4. Application workflow: build, push, and update revisions

[deploy-azure.yml](../../.github/workflows/deploy-azure.yml) is triggered by application-source changes on `main`. It does not reapply Bicep infrastructure.

```mermaid
sequenceDiagram
    autonumber
    participant Push as Push to main
    participant GH as GitHub Actions runner
    participant Entra as Microsoft Entra ID
    participant ARM as Azure Resource Manager
    participant ACR as Azure Container Registry
    participant API as txv-api Container App
    participant Mock as txv-mock Container App

    Push->>GH: Trigger deploy-azure.yml for src/** change
    GH->>Entra: Present GitHub OIDC token for azure-dev
    Entra-->>GH: Short-lived Azure access token

    GH->>ARM: Read txv-infra-dev deployment outputs
    ARM-->>GH: Resource group, ACR, API, Mock names

    GH->>GH: docker build API image on runner
    GH->>ACR: docker push txv-api:git-sha
    ACR-->>GH: txv-api:git-sha pushed
    GH->>GH: docker build Mock image on runner
    GH->>ACR: docker push txv-mock:git-sha
    ACR-->>GH: txv-mock:git-sha pushed

    GH->>ARM: Update txv-api to txv-api:git-sha
    ARM->>API: Create new revision
    API->>ACR: Pull the tagged API image using managed identity
    ACR-->>API: Image layers

    GH->>ARM: Update txv-mock to txv-mock:git-sha
    ARM->>Mock: Create new revision
    Mock->>ACR: Pull the tagged Mock image using managed identity
    ACR-->>Mock: Image layers
```

## 5. Runtime authorization boundaries

The GitHub workflow identity is not the same identity as the running Container Apps. The workflow deploys resources; each Container App's managed identity accesses runtime dependencies.

```mermaid
flowchart LR
    GH["GitHub Actions service principal"]
    GH -->|"Contributor: deploy/update"| Subscription["Azure subscription"]
    GH -->|"User Access Administrator: create runtime role assignments"| ResourceGroup["rg-txv-dev"]

    API["txv-api user-assigned managed identity"]
    API -->|"Service Bus Data Sender"| SB["Azure Service Bus"]
    API -->|"Key Vault Secrets User"| KV["Azure Key Vault"]
    API -->|"AcrPull"| ACR["Azure Container Registry"]

    Mock["txv-mock user-assigned managed identity"]
    Mock -->|"Service Bus Data Receiver"| SB
    Mock -->|"AcrPull"| ACR

    API -->|"Private endpoint"| Redis["Azure Cache for Redis"]
```

## 6. Authentication decision tree

```mermaid
flowchart TD
    Start["Workflow reaches azure/login@v2"] --> Token["GitHub mints OIDC token"]
    Token --> Subject{"Token subject matches<br/>a federated credential?"}
    Subject -->|No| OidcFailure["Login fails: Entra rejects token"]
    Subject -->|Yes| App{"AZURE_CLIENT_ID and<br/>AZURE_TENANT_ID valid?"}
    App -->|No| IdentifierFailure["Login fails: app/tenant not found"]
    App -->|Yes| Role{"Service principal has<br/>required RBAC at scope?"}
    Role -->|No| AuthorizationFailure["Login may succeed, but az deployment/<br/>ACR command is denied"]
    Role -->|Yes| Success["Short-lived Azure token issued;<br/>workflow can perform authorized operations"]
```

## 7. Use cases and expected controls

| Use case | Trigger | Identity used | Required control | Result |
|---|---|---|---|---|
| Preview Bicep | Pull request modifies `infra/bicep/**` | GitHub service principal without a GitHub Environment | Matching `pull_request` federated credential + Contributor | `what-if` reports proposed changes only |
| Apply Bicep | Push to `main` after an infra change | GitHub service principal through `azure-infra` | OIDC + Contributor + User Access Administrator + required reviewer approval | Azure resources are created/updated |
| Deploy application image | Push to `main` modifies `src/**` or manual dispatch | GitHub service principal through `azure-dev` | OIDC + permission to build/push/update | Runner-built images are pushed to ACR; apps receive new revisions |
| API sends a message | Accepted transaction request | `txv-api` user-assigned managed identity | Service Bus Data Sender | Azure Service Bus accepts topic message |
| Mock consumes a message | Service Bus delivery | `txv-mock` user-assigned managed identity | Service Bus Data Receiver | Consumer reads/completes subscription message |
| API reads its API key / Redis config | Container App startup | `txv-api` user-assigned managed identity | Key Vault Secrets User | Key Vault-backed secret becomes Container App config |
| App pulls its image | Revision startup/scale out | API or Mock managed identity | AcrPull | ACR serves the required tagged image |

## 8. What must match exactly

OIDC federation is strict. These values must agree or the workflow login fails:

| GitHub workflow / environment | Entra federated credential |
|---|---|
| `environment: azure-infra` in `infra.yml` apply job | numeric subject ending `environment:azure-infra` |
| No Environment in `infra.yml` what-if job | numeric subject ending `pull_request` |
| `environment: azure-dev` in `deploy-azure.yml` | numeric subject ending `environment:azure-dev` |
| Repository `haudtech/transaction-validation` | subject beginning `repo:haudtech/transaction-validation:` |
| `permissions: id-token: write` | GitHub must be allowed to mint an OIDC token |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` secrets | Existing Entra application and target Azure subscription |

## 9. Operational checklist

- [x] App Registration and service principal exist.
- [x] `azure-infra`, `azure-dev`, and pull-request preview federated credentials exist with matching numeric GitHub subjects.
- [x] Service principal has `Contributor` at subscription scope and `User Access Administrator` at `rg-txv-dev` scope.
- [x] GitHub Environments exist; `azure-infra` requires reviewer approval.
- [x] Required GitHub repository secrets exist.
- [x] Run `infra.yml` successfully through its approved apply job.
- [x] Run `deploy-azure.yml` successfully and confirm both revisions pull their tagged ACR images.
- [x] Complete deployed-environment validation in [azure_deployment_plan.md](../implementation/azure_deployment_plan.md).
- [ ] Run the repository E2E suite and audit-consumer redelivery check from a runner that can reach the Mock app's internal-only ingress.
