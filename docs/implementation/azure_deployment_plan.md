# Azure Deployment Plan

Status: Deployed and validated in the dev environment

Scope: steps required to deploy the TransactionValidation BFF to Azure as a single dev/POC environment, expandable later to dev + staging. This plan was agreed after a point-by-point clarification pass and supersedes ad-hoc deployment notes elsewhere.

## Decisions

| Item | Decision |
|---|---|
| Environments | Single dev/POC environment now; dev + staging later |
| Compute | Azure Container Apps (KEDA scaling, consumption pricing, native fit for mixed HTTP + Service Bus background workers) |
| Service Bus auth | Managed Identity (`DefaultAzureCredential`) for Azure-hosted compute; connection string remains the local-dev path |
| Idempotency store | Azure Cache for Redis, private endpoint only |
| Mock/consumer split | No split; `TransactionValidation.Mock` deploys as-is with its hosted consumer services |
| Infrastructure as code | Bicep |
| CI/CD | Full pipeline: build, push, deploy |
| Networking | Public HTTPS (enforced) for the API; private endpoints for Service Bus and Redis |
| Health checks & logging | Both included |
| Execution order | Identity → Idempotency → Health checks → Prod logging → Infra → Deploy |

## Phase 1 — Managed Identity auth for Azure Service Bus

Status: Done

- [x] Add `Azure.Identity` and `ServiceBusClientFactory` (connection string when present, otherwise `DefaultAzureCredential` against `Namespace`).
- [x] Give `ServiceBusPublisherOptions` / `ServiceBusConsumerOptions` (Mock) an optional `Namespace`; `ConnectionString` is no longer mandatory.
- [x] Update `Program.cs` validation to require connection string **or** namespace.
- [x] Keep local `.env` / Docker Compose flow unaffected — it always uses connection strings; `Namespace` is only meaningful for Azure-hosted compute and is not part of `.env.example`.
- [x] Document role assignments and setup steps in [azure_service_bus_setup.md](azure_service_bus_setup.md#8a-managed-identity-auth-recommended-for-azure-deployment).

## Phase 2 — Redis-backed idempotency store

Status: Done

- [x] Identify that `InMemoryIdempotencyStore` only works correctly for a single replica; Container Apps can scale the API to multiple replicas, which would let duplicate-payload replay and conflict detection break silently per-instance.
- [x] Add `RedisOptions` (`Redis:ConnectionString`) and `RedisIdempotencyStore : IIdempotencyStore` using `StackExchange.Redis` directly (atomic `SET NX` for acquisition, matching the existing TTL/duplicate/conflict semantics).
- [x] Make `Program.cs` select `RedisIdempotencyStore` when `Redis:ConnectionString` is configured, otherwise keep `InMemoryIdempotencyStore` — local dev behavior is unchanged.
- [x] Consolidate the Redis-vs-in-memory decision and `IConnectionMultiplexer` registration into a single `AddIdempotencyStore` method.
- [x] Provision Azure Cache for Redis with a private endpoint only (`infra/bicep/modules/redis.bicep`, Phase 5); no public network access.

## Phase 3 — Health checks

Status: Done

- [x] Add `builder.Services.AddHealthChecks()` in both `TransactionValidation.Api` and `TransactionValidation.Mock`.
- [x] Add a lightweight messaging check (`MessagingHealthCheck` — confirms `IMessagePublisher` resolves for the active broker) and a Redis check (`RedisHealthCheck` — pings Redis when configured, healthy no-op otherwise).
- [x] Add `app.MapHealthChecks("/healthz")` in both apps, ready for Container Apps readiness/liveness probes.
- [x] Fix: exempt `/healthz` from `ApiKeyMiddleware` — found via live end-to-end verification (probes don't send the API key header and were getting `401`).
- [x] Verified end-to-end: ran the API locally and confirmed `GET /healthz` returns `200 Healthy`.

## Phase 4 — Production logging profile

Status: Done

- [x] Removed the `File` sink from the base `appsettings.json` (Api) so Production (default environment when unset) is Console-only; container filesystems are ephemeral.
- [x] Kept the `File` sink in `appsettings.Development.json` only, so local dev logging to `logs/app-.txt` is unchanged.
- [x] Added `appsettings.Production.json` (Api) explicitly documenting the Console-only profile for ops clarity, even though the base file alone already produces this behavior.
- [x] `TransactionValidation.Mock` has no Serilog wiring (default ASP.NET Core logging only), so no change was needed there.
- [x] Verified end-to-end: ran the Api with `ASPNETCORE_ENVIRONMENT=Production`, confirmed `GET /healthz` → `200 Healthy` and no `logs/` directory was created; confirmed Development still writes files as before.
- Note: a plain JSON-array override in `appsettings.Production.json` would **not** have removed the base file sink (later config providers merge arrays by index, they don't shrink them), so the base file itself had to drop the `File` entry rather than relying on a Production-only override.

## Phase 5 — Bicep infrastructure

Status: Done

New `infra/bicep/` with modules:

- [x] `vnet.bicep` — VNet with `snet-infra` (delegated to Container Apps) and `snet-pe` (private endpoints)
- [x] `servicebus.bicep` — topic + primary/audit subscriptions + SQL filters + private endpoint
- [x] `redis.bicep` — Azure Cache for Redis with private endpoint only
- [x] `keyvault.bicep` — secrets (API key, Redis connection string)
- [x] `containerapps.bicep` — Log Analytics, ACR, Container Apps environment, `txv-api` (external HTTPS-only ingress), `txv-mock` (internal-only ingress), system-assigned managed identities
- [x] Role assignments: `Azure Service Bus Data Sender` (API), `Azure Service Bus Data Receiver` (Mock), `Key Vault Secrets User` (API), `AcrPull` (both apps)
- [x] Use **user-assigned** managed identities (one per app) rather than system-assigned. With system-assigned identities the role assignments can only be declared after the app exists (they need its `principalId`), but Container Apps provisions the first revision immediately — so the image pull ran before `AcrPull` was granted and failed with `401 UNAUTHORIZED`, surfacing as `ContainerAppOperationError: Operation expired`. User-assigned identities are created first, granted their roles, and only then referenced by the apps via `dependsOn`. Each app sets `AZURE_CLIENT_ID` so `DefaultAzureCredential` selects the right identity, and the per-app least-privilege split is preserved.
- [x] `main.bicep` (subscription-scope entry point, creates the resource group and wires all five modules) + `main.parameters.dev.json`
- [x] Verified with `az deployment sub what-if` against a real subscription — confirmed the template is valid and produces the expected resource plan.

## Phase 6 — CI/CD pipeline

Status: Done

- [x] Add `.github/workflows/deploy-azure.yml` — triggered on pushes to `main` touching `src/**`; builds both images via `az acr build` (ACR Tasks, no local Docker in the runner) and updates the two Container Apps' revisions with the new image tag (`github.sha`).
- [x] Add `.github/workflows/infra.yml` — separate from app deploys since infra changes are rarer and higher blast radius:
  - on pull requests touching `infra/bicep/**`: runs `az deployment sub what-if` only (preview, no changes)
  - on merge to `main` touching `infra/bicep/**`: runs the real `az deployment sub create`, gated behind the `azure-infra` GitHub environment (configure required reviewers there for manual approval)
  - `workflow_dispatch` on both workflows as a manual escape hatch
- [x] Configure OIDC federated login (`azure/login@v2`) in both workflows — no long-lived secrets stored in GitHub; requires `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` secrets plus a federated credential on the Azure AD app registration (one-time setup, not part of the workflow file itself).
- [x] `API_KEY_SECRET_VALUE` GitHub secret feeds the `@secure()` `apiKeySecretValue` Bicep parameter — never committed to the repo.
- [x] Resource/app names in `deploy-azure.yml` are resolved dynamically from the `infra.yml` deployment's outputs (`az deployment sub show --name txv-infra-dev`) rather than hardcoded — added `apiAppName`/`mockAppName`/`acrName` outputs to `containerapps.bicep` and `main.bicep` for this. The only fixed value shared between the two workflows is the deployment name itself (`txv-infra-dev`), a single deliberate coupling point instead of four duplicated derived strings.

### Prerequisite: one-time Azure AD OIDC setup (blocks both workflows until done)

Status: Done

`AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` are identifiers, not credentials — the actual trust comes from a federated credential. Completed via CLI, step by step, against the real subscription and repo:

- [x] Created Azure AD App Registration `txv-github-actions-oidc` (appId/`AZURE_CLIENT_ID` = `b5ddd0a1-5d22-434e-a518-1ae33af326f2`) and its service principal.
- [x] Assigned `Contributor` at subscription scope (`/subscriptions/31ddc37b-7b65-43a9-b668-0a3796314995`) so `infra.yml` can create the resource group and all resources in `main.bicep`.
- [x] Added federated credentials on the App Registration for both GitHub environments:
  - `github-azure-infra` → subject `repo:haudtech/transaction-validation:environment:azure-infra`
  - `github-azure-dev` → subject `repo:haudtech/transaction-validation:environment:azure-dev`
  - both: issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`
- [x] Added `github-infra-pull-request-preview` for the `infra.yml` `what-if` job, which has no GitHub Environment and presents the repository's `pull_request` OIDC subject. This resolved the `AADSTS700213` subject-mismatch failure on infrastructure-preview runs.
- [x] Added `github-azure-infra-numeric-subject` and `github-azure-dev-numeric-subject` after GitHub emitted numeric owner/repository IDs in the approved apply assertion. This resolved the `AADSTS700213` mismatch for `environment:azure-infra`; the apply job was rerun and is awaiting the existing manual approval gate.
- [x] Granted the deployment service principal `User Access Administrator` scoped to `rg-txv-dev`. `Contributor` alone excludes `Microsoft.Authorization/roleAssignments/write`, so the apply failed while creating the runtime role assignments in `containerapps.bicep`. Scoped to the resource group rather than the subscription to limit the added privilege.
- [x] Created the `azure-infra` and `azure-dev` GitHub environments and configured `haudtech` as a required reviewer on `azure-infra` (manual approval gate before real infra changes apply).
- [x] Stored `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` (`15125a0a-8358-4d0b-b60b-993e7913ff6f`), `AZURE_SUBSCRIPTION_ID` (`31ddc37b-7b65-43a9-b668-0a3796314995`) as GitHub repository secrets.
- [x] Generated a random `API_KEY_SECRET_VALUE` (`openssl rand -base64 32`) and stored it as a GitHub secret directly from the command that generated it — the raw value was never printed to the terminal or chat.

Both workflows can now authenticate via OIDC; nothing else blocks running them.

### Post-deployment issues found during the first live apply

- [x] `MessagingSkuUpgradeNotAllowed` — a manually created Standard-tier Service Bus namespace already occupied `sb-txv-dev-001`, and Azure does not allow an in-place upgrade to the Premium tier that private endpoints require. Deleted the manual namespace so Bicep could create it at the correct tier.
- [x] `ContainerAppOperationError: Operation expired` on `txv-mock-dev` — container logs showed `ACR token exchange endpoint returned error status: 401 UNAUTHORIZED`. Root cause was the system-assigned identity ordering problem fixed in Phase 5; resolved by switching to user-assigned identities.
- [x] `TasksOperationsNotAllowed` in `deploy-azure.yml` — ACR Tasks (`az acr build`) is blocked on this subscription, a restriction Azure applies to new subscriptions and which only Azure support can lift. Switched the workflow to build images on the GitHub runner with `docker build` and push to ACR, which needs no subscription-level exemption.

## Phase 7 — Validation

Status: Done

Verified against the live dev environment (`txv-api-dev.wittyfield-669bab78.eastus.azurecontainerapps.io`) on 2026-09-07:

- [x] Confirmed `/healthz` returns `200 Healthy`. This also proves `MessagingHealthCheck` resolved the Service Bus publisher and `RedisHealthCheck` reached Redis over its private endpoint.
- [x] Confirmed Key Vault-sourced configuration resolves end to end: the API authenticates callers with the Key Vault-backed `Security--ApiKey` secret and uses the Key Vault-backed Redis connection string, both delivered through the user-assigned identity.
- [x] Confirmed the accepted-transaction path returns `202` with a `messageId`, exercising API key auth, validation, idempotency, partner verification against the internal-only Mock app, and a Managed Identity publish to Service Bus.
- [x] Confirmed both consumers observed the same `MessageId` on their own subscriptions, with the audit consumer receiving `RoutingKey=partner.transaction.accepted`. Dead-letter counts stayed at `0`.
- [x] Confirmed Redis-backed idempotency across replicas: scaled the API to `minReplicas: 2`, sent 16 identical requests with one `Idempotency-Key`, and received exactly one distinct `messageId`. A per-instance in-memory store would have produced one id per replica. Scale restored to `minReplicas: 1` afterwards.
- [x] Confirmed error semantics: `409` for an idempotency key reused with a different payload, `400` with ProblemDetails for an invalid payload, and `401` for a missing or incorrect API key.
- [ ] Run the repository `test:e2e` suite against the deployed environment. The suite's fixture publishes directly to Service Bus and reads the Mock observation endpoint, but the Mock app uses internal-only ingress, so it is unreachable from a developer machine. Running it requires either temporary external ingress on the Mock app or a runner inside the VNet.
- [ ] Exercise the audit-consumer failure-before-completion redelivery path in Azure. It relies on the Mock test-support endpoint, which is blocked by the same internal-ingress constraint.

Note: `Key Vault Secrets User` was granted to the developer account on `kv-txv-dev` to read the API key during this validation. Remove it if strict least privilege is desired; the Container Apps do not depend on it.

## Overall checklist

- [x] Phase 1 — Managed Identity auth for Azure Service Bus
- [x] Phase 2 — Redis-backed idempotency store
- [x] Phase 3 — Health checks
- [x] Phase 4 — Production logging profile
- [x] Phase 5 — Bicep infrastructure
- [x] Phase 6 — CI/CD pipeline
- [x] Phase 7 — Validation (two optional checks remain blocked by the Mock app's internal-only ingress)
