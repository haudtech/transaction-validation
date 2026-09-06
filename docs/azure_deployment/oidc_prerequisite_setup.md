# Azure AD OIDC Prerequisite Setup — Runbook

Status: Completed for the `azure-infra` / `azure-dev` dev environment on 2026-09-04.

This document records the exact commands run to satisfy the "Prerequisite: one-time Azure AD OIDC setup" checklist in [azure_deployment_plan.md](../implementation/azure_deployment_plan.md), including the expected result for each step. It doubles as a runbook for repeating this setup for a future `staging`/`prod` environment.

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

Values captured for this environment:

| Value | Result |
|---|---|
| `AZURE_SUBSCRIPTION_ID` | `31ddc37b-7b65-43a9-b668-0a3796314995` |
| `AZURE_TENANT_ID` | `15125a0a-8358-4d0b-b60b-993e7913ff6f` |
| Signed-in user | `haud.tech@gmail.com` (type: `user`) |

## Step 3 — Confirm GitHub CLI login and exact repo slug

```bash
gh auth status
git remote -v
```

**Expected result:** `gh auth status` shows `✓ Logged in to github.com` with `repo` and `workflow` token scopes (both required to manage secrets/environments). `git remote -v` shows the `origin` URL, from which the `org/repo` slug is read.

Value captured: repo slug = `haudtech/transaction-validation`.

This value feeds the federated credential `subject` claims in Step 7–8, which must be exactly `repo:haudtech/transaction-validation:environment:<env-name>`.

## Step 4 — Create the Azure AD App Registration

```bash
az ad app create --display-name "txv-github-actions-oidc" -o json
```

**Expected result:** JSON for the new application, including `appId` (this becomes `AZURE_CLIENT_ID`) and `id` (the Azure AD object ID, distinct from `appId`).

Values captured:

| Value | Result |
|---|---|
| `AZURE_CLIENT_ID` (`appId`) | `b5ddd0a1-5d22-434e-a518-1ae33af326f2` |
| App object ID (`id`) | `88d77ed7-4871-4666-96e6-869797edcecc` |

## Step 5 — Create the service principal for the App Registration

App registrations cannot be assigned RBAC roles directly — they need a linked service principal first.

```bash
az ad sp create --id b5ddd0a1-5d22-434e-a518-1ae33af326f2 -o json
```

**Expected result:** JSON for the new service principal, including its own `id` (service principal object ID, used as the `--assignee` in Step 6) and `appId` matching the App Registration's client ID.

Value captured: service principal object ID = `2eaa30b1-6f9f-4a58-b5c4-f4cb2395ee73`.

## Step 6 — Assign the Contributor role at subscription scope

```bash
az role assignment create \
  --assignee b5ddd0a1-5d22-434e-a518-1ae33af326f2 \
  --role Contributor \
  --scope /subscriptions/31ddc37b-7b65-43a9-b668-0a3796314995 \
  -o json
```

**Expected result:** JSON confirming the role assignment, with `principalType: ServicePrincipal` and `roleDefinitionId` pointing to the built-in `Contributor` role GUID (`b24988ac-6180-42a0-ab88-20f7382dd24c`).

Subscription scope is required (not resource-group scope) because `infra.yml` runs `az deployment sub create`, which creates the resource group itself — a role scoped only to a resource group that doesn't exist yet would fail.

## Step 7 — Add the federated credential for the `azure-infra` environment

```bash
az ad app federated-credential create \
  --id b5ddd0a1-5d22-434e-a518-1ae33af326f2 \
  --parameters '{
    "name": "github-azure-infra",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:haudtech/transaction-validation:environment:azure-infra",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** JSON echoing back the created federated credential, including its own `id`, `issuer`, `subject`, and `audiences`. This is what lets `infra.yml`'s jobs running under the `azure-infra` GitHub environment authenticate via OIDC.

## Step 8 — Add the federated credential for the `azure-dev` environment

Same command, different `name`/`subject`:

```bash
az ad app federated-credential create \
  --id b5ddd0a1-5d22-434e-a518-1ae33af326f2 \
  --parameters '{
    "name": "github-azure-dev",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:haudtech/transaction-validation:environment:azure-dev",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Expected result:** same shape as Step 7, with `subject` ending in `environment:azure-dev`. This covers `deploy-azure.yml`.

## Step 9 — Create the two GitHub environments

```bash
gh api repos/haudtech/transaction-validation/environments/azure-infra -X PUT --silent
gh api repos/haudtech/transaction-validation/environments/azure-dev -X PUT --silent
gh api repos/haudtech/transaction-validation/environments -q '.environments[].name'
```

**Expected result:** the first two commands produce no output on success (`--silent`, HTTP 200/201). The third lists all environments in the repo; `azure-dev` and `azure-infra` should both appear (alongside any pre-existing environments, e.g. `integration`).

## Step 10 — Configure required reviewer on `azure-infra`

```bash
gh api /user -q '.id, .login'
```

**Expected result:** your numeric GitHub user ID and login, needed for the next call.

```bash
gh api repos/haudtech/transaction-validation/environments/azure-infra -X PUT \
  -f "reviewers[][type]=User" \
  -F "reviewers[][id]=110943608" \
  --silent

gh api repos/haudtech/transaction-validation/environments/azure-infra \
  -q '.protection_rules[].reviewers[].reviewer.login'
```

**Expected result:** the `PUT` produces no output on success. The verification `GET` prints the reviewer's login (`haudtech`), confirming the manual-approval gate is active — `infra.yml`'s `apply` job will now pause for approval before running `az deployment sub create`.

**Pitfall hit during this run:** including `-f "deployment_branch_policy="` in the same request fails with `HTTP 422: Invalid property /deployment_branch_policy: "" is not of type object` — that field must be omitted entirely (not set to an empty string) when not configuring branch restrictions.

## Step 11 — Store the three identifier secrets

```bash
gh secret set AZURE_CLIENT_ID -b "b5ddd0a1-5d22-434e-a518-1ae33af326f2"
gh secret set AZURE_TENANT_ID -b "15125a0a-8358-4d0b-b60b-993e7913ff6f"
gh secret set AZURE_SUBSCRIPTION_ID -b "31ddc37b-7b65-43a9-b668-0a3796314995"
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
| App Registration | `txv-github-actions-oidc` (`AZURE_CLIENT_ID = b5ddd0a1-5d22-434e-a518-1ae33af326f2`) |
| Service principal | object ID `2eaa30b1-6f9f-4a58-b5c4-f4cb2395ee73` |
| RBAC role | `Contributor` at subscription scope `31ddc37b-7b65-43a9-b668-0a3796314995` |
| Federated credentials | `github-azure-infra`, `github-azure-dev` (both issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`) |
| GitHub environments | `azure-infra` (required reviewer: `haudtech`), `azure-dev` |
| GitHub secrets | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `API_KEY_SECRET_VALUE` |

Both [infra.yml](../../.github/workflows/infra.yml) and [deploy-azure.yml](../../.github/workflows/deploy-azure.yml) can now authenticate via OIDC. Nothing else blocks running them.

## Repeating this for a future environment (e.g. `staging`)

1. Reuse the same App Registration and service principal (Steps 4–6 do not need to be repeated) — or create a dedicated one per environment if stricter isolation is desired.
2. Repeat Steps 7–8 with a new federated credential whose `subject` matches the new GitHub environment name (e.g. `environment:azure-staging`).
3. Repeat Step 9 to create the new GitHub environment, and Step 10 if it also needs a manual-approval gate.
4. Secrets from Step 11–12 are repository-level and already shared across environments; no change needed unless staging uses a different subscription.
