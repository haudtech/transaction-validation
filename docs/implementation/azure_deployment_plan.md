# Azure Deployment Plan

Status: In progress — Phases 1, 2 and 3 done, Phase 4 next

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
- [ ] Provision Azure Cache for Redis with a private endpoint only (Phase 5); no public network access.

## Phase 3 — Health checks

Status: Done

- [x] Add `builder.Services.AddHealthChecks()` in both `TransactionValidation.Api` and `TransactionValidation.Mock`.
- [x] Add a lightweight messaging check (`MessagingHealthCheck` — confirms `IMessagePublisher` resolves for the active broker) and a Redis check (`RedisHealthCheck` — pings Redis when configured, healthy no-op otherwise).
- [x] Add `app.MapHealthChecks("/healthz")` in both apps, ready for Container Apps readiness/liveness probes.
- [x] Fix: exempt `/healthz` from `ApiKeyMiddleware` — found via live end-to-end verification (probes don't send the API key header and were getting `401`).
- [x] Verified end-to-end: ran the API locally and confirmed `GET /healthz` returns `200 Healthy`.

## Phase 4 — Production logging profile

Status: Not started

- [ ] Add `appsettings.Production.json` (Api + Mock) with a Console-only Serilog sink; drop the `File` sink since container filesystems are ephemeral.
- [ ] Confirm Container Apps captures stdout to Log Analytics; Azure Monitor / Application Insights wiring already exists via `UseAzureMonitor` in `ServiceCollectionExtensions`.

## Phase 5 — Bicep infrastructure

Status: Not started

New `infra/bicep/` with modules:

- [ ] `vnet.bicep` — required for private endpoints
- [ ] `servicebus.bicep` — topic + primary/audit subscriptions + SQL filters + private endpoint
- [ ] `redis.bicep` — Azure Cache for Redis with private endpoint only
- [ ] `keyvault.bicep` — secrets (API key, any remaining connection strings)
- [ ] `containerapps.bicep` — Container Apps environment, `txv-api` (external HTTPS-only ingress), `txv-mock` (internal-only ingress), system-assigned managed identities
- [ ] Role assignments: `Azure Service Bus Data Sender` (API), `Azure Service Bus Data Receiver` (Mock), `Key Vault Secrets User` (both)

## Phase 6 — CI/CD pipeline

Status: Not started

- [ ] Add `.github/workflows/deploy-azure.yml`.
- [ ] Configure OIDC federated login (`azure/login@v2`) — no long-lived secrets stored in GitHub.
- [ ] Build/push images to Azure Container Registry.
- [ ] Run `az deployment group create` for the Bicep templates, then update Container Apps revisions.

## Phase 7 — Validation

Status: Not started

- [ ] Run the existing `test:e2e` task against the deployed dev environment with `MESSAGING__BROKERTYPE=AzureServiceBus`.
- [ ] Confirm `/healthz` on both apps.
- [ ] Confirm Redis-backed idempotency replay across at least 2 API replicas.
- [ ] Confirm Key Vault-sourced configuration end to end before calling the deployment done.

## Overall checklist

- [x] Phase 1 — Managed Identity auth for Azure Service Bus
- [x] Phase 2 — Redis-backed idempotency store
- [x] Phase 3 — Health checks
- [ ] Phase 4 — Production logging profile
- [ ] Phase 5 — Bicep infrastructure
- [ ] Phase 6 — CI/CD pipeline
- [ ] Phase 7 — Validation
