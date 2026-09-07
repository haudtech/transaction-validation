# Azure AD OIDC Prerequisite Setup — Runbook

Status: Completed for the `azure-infra` / `azure-dev` dev environment on 2026-09-07. The deployed dev environment was validated; the optional repository E2E suite remains blocked because the Mock app has internal-only ingress.

This document records the exact commands run to satisfy the "Prerequisite: one-time Azure AD OIDC setup" checklist in [azure_deployment_plan.md](../implementation/azure_deployment_plan.md), including the expected result for each step. It doubles as a runbook for repeating this setup for a future `staging`/`prod` environment.

Replace every placeholder in angle brackets before running a command. The values are intentionally omitted from this reusable guide:

- `<AZURE_SUBSCRIPTION_ID>` — target Azure subscription GUID.
- `<AZURE_TENANT_ID>` — Microsoft Entra tenant GUID.
- `<AZURE_CLIENT_ID>` — App Registration application/client ID.
- `<AZURE_APP_OBJECT_ID>` — App Registration object ID, when needed.
- `<AZURE_SERVICE_PRINCIPAL_OBJECT_ID>` — service principal object ID, when needed.
- `<GITHUB_OWNER>/<GITHUB_REPOSITORY>` — repository slug.
- `<GITHUB_OWNER_ID>` and `<GITHUB_REPOSITORY_ID>` — numeric GitHub owner/repository IDs used by emitted OIDC subjects.
- `<GITHUB_REVIEWER_ID>` and `<GITHUB_REVIEWER_LOGIN>` — required environment reviewer identity.

## Which steps are required again?

If only the Azure resource group was deleted, do **not** repeat this entire runbook. The Entra App Registration, service principal, federated credentials, GitHub environments, GitHub secrets, and subscription-level `Contributor` assignment are outside the resource group and should still exist.

Before triggering the infrastructure workflow again, verify or recreate only:

1. The target resource group, because it was deleted. `main.bicep` declares the resource group, but it must exist before a resource-group-scoped role can be assigned.
2. The `User Access Administrator` assignment for the deployment service principal at the target resource-group scope, because that assignment was deleted with the resource group.
3. The GitHub OIDC secrets and environment protection rules, if they were changed or deleted independently.

Then follow the recovery procedure below, run the infrastructure workflow, and approve the `azure-infra` environment. Complete Steps 1–12 in full only for a new repository, a new Azure tenant/subscription, or an environment whose OIDC setup was also removed.

### Recovery after resource-group deletion

Replace the placeholders before running these commands. Run them after the resource-group deletion has completed:

```bash
SUBSCRIPTION_ID=<AZURE_SUBSCRIPTION_ID>
CLIENT_ID=<AZURE_CLIENT_ID>
RESOURCE_GROUP=<AZURE_RESOURCE_GROUP>
LOCATION=<AZURE_LOCATION>

az account set --subscription "$SUBSCRIPTION_ID"

az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

az role assignment create \
  --assignee "$CLIENT_ID" \
  --role "User Access Administrator" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"
```

Verify the existing non-resource-group prerequisites before dispatching the workflow:

```bash
az ad app show --id "$CLIENT_ID" --query "{appId:appId, displayName:displayName}" -o table
az role assignment list \
  --assignee "$CLIENT_ID" \
  --scope "/subscriptions/$SUBSCRIPTION_ID" \
  --role Contributor \
  -o table

gh secret list
gh api "repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments" \
  -q '.environments[].name'
```

The expected GitHub output includes the four repository secrets and the `azure-infra` / `azure-dev` environments. The App Registration and subscription-level `Contributor` assignment must also be present.

Dispatch the infrastructure workflow from `main`:

```bash
gh workflow run infra.yml --ref main
gh run list --workflow infra.yml --limit 1
```

Approve the `azure-infra` environment when the run pauses. The workflow recreates the remaining Azure resources through `main.bicep`. After it succeeds, dispatch `deploy-azure.yml` or push an application change to `main` to build and deploy the API and Mock images.

Related workflows that depend on this setup:

- [.github/workflows/infra.yml](../../.github/workflows/infra.yml)
- [.github/workflows/deploy-azure.yml](../../.github/workflows/deploy-azure.yml)

## Why this setup is required

`AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` are identifiers, not credentials. The actual trust between GitHub Actions and Azure comes from an OIDC **federated credential** on an Azure AD App Registration — GitHub presents a signed token at workflow run time, and Azure AD trusts it only if the token's issuer and subject match a federated credential configured here. No client secret or password is stored anywhere.

## Step 1 — Confirm Azure CLI login and target subscription

```bash
az account show -o table
```

**Expected result:** a table showing `IsDefault: True`, the subscription name, and `State: Enabled`. Confirms `az` commands will run against the correct subscription without needing `az account set`.

## Step 2 — Get the exact subscription ID and tenant ID

```bash
az account show -o json
```

**Expected result:** JSON containing `id` (subscription ID), `tenantId`, and `user.name`/`user.type`. Record these — they map directly to `AZURE_SUBSCRIPTION_ID` and `AZURE_TENANT_ID`.

| Value | Result |
|---|---|
| `AZURE_SUBSCRIPTION_ID` | `<AZURE_SUBSCRIPTION_ID>` |
| `AZURE_TENANT_ID` | `<AZURE_TENANT_ID>` |
| Signed-in user | `<AZURE_SIGNED_IN_USER>` |

## Step 3 — Confirm GitHub CLI login and exact repo slug

```bash
gh auth status
git remote -v
```

**Expected result:** `gh auth status` shows `✓ Logged in to github.com` with `repo` and `workflow` token scopes (both required to manage secrets/environments). `git remote -v` shows the `origin` URL, from which the `org/repo` slug is read.

Repository slug: `<GITHUB_OWNER>/<GITHUB_REPOSITORY>`.

This value feeds the federated credential `subject` claims in Step 7–8, which must be exactly `repo:<GITHUB_OWNER>/<GITHUB_REPOSITORY>:environment:<env-name>`.

## Step 4 — Create the Azure AD App Registration

```bash
az ad app create --display-name "txv-github-actions-oidc" -o json
```

**Expected result:** JSON for the new application, including `appId` (this becomes `AZURE_CLIENT_ID`) and `id` (the Azure AD object ID, distinct from `appId`).

| Value | Result |
|---|---|
| `AZURE_CLIENT_ID` (`appId`) | `<AZURE_CLIENT_ID>` |
| App object ID (`id`) | `<AZURE_APP_OBJECT_ID>` |

## Step 5 — Create the service principal for the App Registration

App registrations cannot be assigned RBAC roles directly — they need a linked service principal first.

```bash
az ad sp create --id <AZURE_CLIENT_ID> -o json
```

**Expected result:** JSON for the new service principal, including its own `id` (service principal object ID, used as the `--assignee` in Step 6) and `appId` matching the App Registration's client ID.

Service principal object ID: `<AZURE_SERVICE_PRINCIPAL_OBJECT_ID>`.

## Step 6 — Assign deployment permissions

```bash
az role assignment create \
  --assignee <AZURE_CLIENT_ID> \
  --role Contributor \
  --scope /subscriptions/<AZURE_SUBSCRIPTION_ID> \
  -o json
```

**Expected result:** JSON confirming the role assignment, with `principalType: ServicePrincipal` and `roleDefinitionId` pointing to the built-in `Contributor` role GUID (`b24988ac-6180-42a0-ab88-20f7382dd24c`).

Subscription scope is required (not resource-group scope) because `infra.yml` runs `az deployment sub create`, which creates the resource group itself — a role scoped only to a resource group that doesn't exist yet would fail.

The Bicep template also creates Azure RBAC assignments for the Container Apps identities. `Contributor` does not include `Microsoft.Authorization/roleAssignments/write`, so grant the deployment service principal `User Access Administrator` on the target resource group as an additional, narrower permission. The resource group must already exist; create it first if this is a new environment:

```bash
az group create \
  --name <AZURE_RESOURCE_GROUP> \
  --location eastus

az role assignment create \
  --assignee <AZURE_CLIENT_ID> \
  --role "User Access Administrator" \
  --scope /subscriptions/<AZURE_SUBSCRIPTION_ID>/resourceGroups/<AZURE_RESOURCE_GROUP> \
  -o json
```

**Expected result:** JSON confirming a `User Access Administrator` assignment at `/subscriptions/<AZURE_SUBSCRIPTION_ID>/resourceGroups/<AZURE_RESOURCE_GROUP>`. Keep this assignment resource-group scoped rather than granting it at subscription scope.

## Step 7 — Add the federated credential for the `azure-infra` environment

```bash
az ad app federated-credential create \
  --id <AZURE_CLIENT_ID> \
  --parameters '{
    "name": "github-azure-infra",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<GITHUB_OWNER>/<GITHUB_REPOSITORY>:environment:azure-infra",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** JSON echoing back the created federated credential, including its own `id`, `issuer`, `subject`, and `audiences`. This is what lets `infra.yml`'s jobs running under the `azure-infra` GitHub environment authenticate via OIDC.

## Step 8 — Add the federated credential for the `azure-dev` environment

Same command, different `name`/`subject`:

```bash
az ad app federated-credential create \
  --id <AZURE_CLIENT_ID> \
  --parameters '{
    "name": "github-azure-dev",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<GITHUB_OWNER>/<GITHUB_REPOSITORY>:environment:azure-dev",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** same shape as Step 7, with `subject` ending in `environment:azure-dev`. This covers `deploy-azure.yml`.

### GitHub numeric subject format

GitHub emitted numeric owner/repository IDs for the actual workflow assertions, for example:

```text
repo:<GITHUB_OWNER>@<GITHUB_OWNER_ID>/<GITHUB_REPOSITORY>@<GITHUB_REPOSITORY_ID>:environment:azure-infra
```

The initial human-readable environment subjects did not match this assertion and the approved `apply` job failed with `AADSTS700213`. Add credentials matching the exact emitted subject for both environments:

```bash
az ad app federated-credential create \
  --id <AZURE_CLIENT_ID> \
  --parameters '{
    "name": "github-azure-infra-numeric-subject",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<GITHUB_OWNER>@<GITHUB_OWNER_ID>/<GITHUB_REPOSITORY>@<GITHUB_REPOSITORY_ID>:environment:azure-infra",
    "audiences": ["api://AzureADTokenExchange"]
  }'

az ad app federated-credential create \
  --id <AZURE_CLIENT_ID> \
  --parameters '{
    "name": "github-azure-dev-numeric-subject",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<GITHUB_OWNER>@<GITHUB_OWNER_ID>/<GITHUB_REPOSITORY>@<GITHUB_REPOSITORY_ID>:environment:azure-dev",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** both commands return a federated-credential record. Always copy the subject shown in an `AADSTS700213` workflow error rather than assuming a human-readable repository format.

## Step 8a — Add the federated credential for pull-request infrastructure previews

The `what-if` job in `infra.yml` does not declare a GitHub Environment. GitHub therefore emits a `pull_request` subject rather than the `environment:azure-infra` subject used by the approved apply job. Entra ID requires an exact subject match, so the preview requires its own federated credential.

```bash
az ad app federated-credential create \
  --id <AZURE_CLIENT_ID> \
  --parameters '{
    "name": "github-infra-pull-request-preview",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<GITHUB_OWNER>@<GITHUB_OWNER_ID>/<GITHUB_REPOSITORY>@<GITHUB_REPOSITORY_ID>:pull_request",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** JSON for `github-infra-pull-request-preview` with the exact `pull_request` subject. The numeric IDs in this subject are GitHub's stable owner and repository identifiers; use the subject reported in `AADSTS700213` if the repository is transferred or the workflow reports a different subject.

This credential authorizes the preview identity only. The workflow job runs `az deployment sub what-if`, not `az deployment sub create`; the real infrastructure apply remains protected by the `azure-infra` Environment approval gate.

## Step 9 — Create the two GitHub environments

```bash
gh api repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments/azure-infra -X PUT --silent
gh api repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments/azure-dev -X PUT --silent
gh api repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments -q '.environments[].name'
```

**Expected result:** the first two commands produce no output on success (`--silent`, HTTP 200/201). The third lists all environments in the repo; `azure-dev` and `azure-infra` should both appear (alongside any pre-existing environments, e.g. `integration`).

## Step 10 — Configure required reviewer on `azure-infra`

```bash
gh api /user -q '.id, .login'
```

**Expected result:** your numeric GitHub user ID and login, needed for the next call.

```bash
gh api repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments/azure-infra -X PUT \
  -f "reviewers[][type]=User" \
  -F "reviewers[][id]=<GITHUB_REVIEWER_ID>" \
  --silent

gh api repos/<GITHUB_OWNER>/<GITHUB_REPOSITORY>/environments/azure-infra \
  -q '.protection_rules[].reviewers[].reviewer.login'
```

**Expected result:** the `PUT` produces no output on success. The verification `GET` prints `<GITHUB_REVIEWER_LOGIN>`, confirming the manual-approval gate is active — `infra.yml`'s `apply` job will now pause for approval before running `az deployment sub create`.

**Pitfall hit during this run:** including `-f "deployment_branch_policy="` in the same request fails with `HTTP 422: Invalid property /deployment_branch_policy: "" is not of type object` — that field must be omitted entirely (not set to an empty string) when not configuring branch restrictions.

## Step 11 — Store the three identifier secrets

```bash
gh secret set AZURE_CLIENT_ID -b "<AZURE_CLIENT_ID>"
gh secret set AZURE_TENANT_ID -b "<AZURE_TENANT_ID>"
gh secret set AZURE_SUBSCRIPTION_ID -b "<AZURE_SUBSCRIPTION_ID>"
gh secret list
```

**Expected result:** three `✓ Set Actions secret ...` confirmation lines, then `gh secret list` showing all three names with a recent `UPDATED` timestamp (secret values are never displayed by `gh secret list`).

## Step 12 — Generate and store the real API key secret

Unlike the three identifiers above, this is a genuine secret value (used for `Security:ApiKey` / the `@secure()` `apiKeySecretValue` Bicep parameter) — generated fresh and stored without ever being printed to the terminal or chat.

```bash
API_KEY_VALUE=$(openssl rand -base64 32)
gh secret set API_KEY_SECRET_VALUE -b "$API_KEY_VALUE"
unset API_KEY_VALUE
gh secret list
```

**Expected result:** `✓ Set Actions secret API_KEY_SECRET_VALUE ...`, and `gh secret list` now shows all four secrets: `API_KEY_SECRET_VALUE`, `AZURE_CLIENT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_TENANT_ID`.

## Final state

| Item | Value / Status |
|---|---|
| App Registration | `txv-github-actions-oidc` (`AZURE_CLIENT_ID = <AZURE_CLIENT_ID>`) |
| Service principal | object ID `<AZURE_SERVICE_PRINCIPAL_OBJECT_ID>` |
| RBAC roles | `Contributor` at subscription scope `<AZURE_SUBSCRIPTION_ID>`; `User Access Administrator` at resource-group scope `<AZURE_RESOURCE_GROUP>` |
| Federated credentials | `github-azure-infra`, `github-azure-dev`, `github-azure-infra-numeric-subject`, `github-azure-dev-numeric-subject`, `github-infra-pull-request-preview` (all issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`) |
| GitHub environments | `azure-infra` (required reviewer: `<GITHUB_REVIEWER_LOGIN>`), `azure-dev` |
| GitHub secrets | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `API_KEY_SECRET_VALUE` |

Both [infra.yml](../../.github/workflows/infra.yml) and [deploy-azure.yml](../../.github/workflows/deploy-azure.yml) authenticate via OIDC, and the dev deployment has been validated. The repository E2E suite and the audit-consumer redelivery check still require a runner that can reach the Mock app's internal-only ingress; they are optional follow-up validation, not OIDC prerequisites.

## Repeating this for a future environment (e.g. `staging`)

1. Reuse the same App Registration and service principal (Steps 4–6 do not need to be repeated) — or create a dedicated one per environment if stricter isolation is desired.
2. Repeat Steps 7–8 with a new federated credential whose `subject` matches the new GitHub environment name (e.g. `environment:azure-staging`).
3. Repeat Step 9 to create the new GitHub environment, and Step 10 if it also needs a manual-approval gate.
4. Secrets from Step 11–12 are repository-level and already shared across environments; no change needed unless staging uses a different subscription.
