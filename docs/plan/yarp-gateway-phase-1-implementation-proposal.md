# YARP Gateway Phase 1 — Implementation Proposal

## Status and approval boundary

**Status: Slices 1–3, 4A, and the Gateway authentication/authorization implementation in Slice 4 are present.** On 2026-11-09 the owner approved the proposed project names, direct Gateway HTTPS as the local TLS boundary, versioned Keycloak plus local-only bootstrap, retaining direct API tests while adding gateway-backed E2E coverage, and the narrow Slice 2 scope. The owner approved proceeding with Slice 4. Targeted Gateway tests pass; human governance review and Docker-backed Gateway/API E2E remain pending. Subsequent work remains gated.

Use `[ ]` for pending work and change to `[x]` only after completing and verifying the item; add evidence or a link beside the item when useful. Gateway-side client authentication, credential lifecycle checks, API/version authorization, and trusted-header handling for Slice 4 are implemented and covered by targeted tests. Compose-backed E2E and human governance review remain pending; management behavior and deployment behavior are not part of this slice.

## Request and acceptance criteria

Build a local Docker-first YARP gateway and private management API, separate from the existing TransactionValidation API. Prove the public partner request route through the gateway to the current backend, while preserving the backend’s business validation, idempotency, partner verification, and message publication behavior.

Phase 1 is accepted when all of the following are checked:

- [ ] Full local Compose and the IDE-friendly infrastructure-only profile start reproducibly.
- [ ] The public gateway route `/transactions/v1/partner/transactions` rewrites to the existing API route `/api/v1/partner/transactions`.
- [ ] The gateway authenticates an explicitly provisioned client key, authorizes it for TransactionValidation v1, and does not forward client credentials.
- [ ] The API authenticates the gateway with a separate credential and requires the trusted client-ID header.
- [ ] Management OIDC, key/policy lifecycle, one-time secret display, auditing, and optimistic concurrency work locally.
- [ ] The local Compose client has explicit finite limits enforced atomically through Redis in Phase 1; Phase 2 generalizes and hardens this capability.
- [ ] Local service links use HTTPS/TLS as clarified; sensitive values and private keys remain outside source control.
- [ ] Existing backend behavior and tests continue to work through the gateway-backed topology or through explicitly retained non-gateway test paths.

## Scope and non-goals

### In scope

- Add the new YARP Gateway and Management hosts at `net10.0`. The existing solution's .NET 10 upgrade (SDK pin, all project targets, CI, Docker, and developer tasks) is already done; see Gate 0.0.
- Local Compose full-stack and infrastructure-only profiles.
- Keycloak OIDC for local management API testing.
- PostgreSQL-backed client/API policy and append-only audit data; EF Core migrations.
- Minimum atomic Redis-backed token-bucket rate limits and daily/monthly UTC quotas in Phase 1, with Phase 2 reserved for generalization and hardening.
- Provider-neutral secret-provider interface with local implementation.
- YARP route rewrite, API-key-first client authentication, API/version authorization, and separate gateway-to-backend credentials.
- Backend API compatibility update and tests.
- CI, test, solution, Docker, and documentation integration required to build and verify new projects.

### Out of scope

- AWS and Azure deployment resources, cloud secret-provider implementations, DNS cutover, and production sizing.
- Gateway management UI, dual administrator approval, or provider-specific admin-auth logic.
- Partner OAuth2/JWT login; API keys remain first, behind a provider-neutral client-authentication interface.
- mTLS or request signing for gateway-to-backend; V1 uses private HTTPS and a separate rotated credential.
- Gateway retries, business idempotency, body buffering/transformation, WebSockets/HTTP upgrades, HTTP/3, and cross-cloud active-active.
- Numeric production rate/quota, timeout, body/header, retention, RPO, and RTO values unless explicitly approved as local development values.

## Current implementation facts to preserve

- [global.json](../../global.json) pins SDK 10.0.301 with `latestPatch`; [Directory.Build.props](../../Directory.Build.props) defaults projects to `net10.0`, and every existing project targets `net10.0`.
- [TransactionValidation.sln](../../TransactionValidation.sln), [central package versions](../../Directory.Packages.props), CI, integration CI, Dockerfiles, and VS Code tasks are already on .NET 10. Actual CI runs and Gateway/Management image builds are reserved for final Phase 1 verification.
- [docker-compose.yml](../../docker-compose.yml) currently runs API, Mock, RabbitMQ, and Redis. The API depends on Mock and RabbitMQ as well as Redis; preserve those dependencies in the proposed full stack.
- The backend route is `POST /api/v1/partner/transactions`. Gateway mode validates configured `X-Gateway-Key` and `X-Authenticated-Client-Id` values; explicit `DirectApiKey` mode is restricted to Development for local E2E and direct API tests.
- Slice 4A added `Gateway.PolicyStore` with EF Core 10.0.4/Npgsql EF 10.0.3 and a migration for clients, key verifier metadata, and API/version grants. It also added the `Gateway.Secrets` local-file adapter; neither is wired into client authentication yet.
- Existing tests directly exercise the API using explicit `DirectApiKey` test-host configuration. Gateway-backed E2E coverage remains a later slice.

## Component ownership and projects

The approved project names are now present in the solution.

| Component | Target | Owns |
|---|---|---|
| `TransactionValidation.Gateway` | `net10.0` | YARP host, configured route matching/rewrites, client-key validation, API/version authorization, header sanitation, correlation, gateway errors, readiness, logs, and metrics. |
| `TransactionValidation.Gateway.Management` | `net10.0` | Private management HTTP API, OIDC scope authorization, client/key/policy/backend-credential operations, audit writes, and administrative validation. |
| `TransactionValidation.Gateway.Contracts` | `net10.0` | Stable backend-auth, client-credential-store, and secret-provider contracts; no ASP.NET Core host types. |
| `TransactionValidation.Gateway.PolicyStore` | `net10.0`; EF Core/Npgsql adapter | PostgreSQL policy schema and persistence implementation shared where needed by Gateway and Management. |
| `TransactionValidation.Gateway.Secrets` | `net10.0` | Local-only file adapter for the provider-neutral secret contract; cloud adapters remain deferred. |
| `TransactionValidation.Api` | `net10.0` (already upgraded) | Existing transaction behavior plus gateway backend-credential and client-ID validation in the gateway-backed topology. |

The gateway and management hosts must not reference the executable API project. The existing API must depend only on an agreed backend-auth contract and verifier implementation. Shared contracts belong in `Gateway.Contracts`; EF Core/Npgsql persistence belongs in `Gateway.PolicyStore`.

## Ordered implementation work plan

### Gate 0 — Validate SDK, package, and backend integration compatibility

Before feature implementation:

- [x] Confirm the .NET 10 SDK pin/roll-forward and CI/Docker configuration.
	- [x] Local: `global.json` pins SDK 10.0.301 with `latestPatch`; restore, Release build (0 warnings, 0 errors), and `dotnet format --verify-no-changes` pass.
	- [x] Configured: CI and integration workflows install `10.0.x`; API/Mock Dockerfiles use `sdk:10.0.301` and `aspnet:10.0`. The owner confirmed Docker/.NET 10 compatibility.
- [x] Prove that the whole solution, including the new `net10.0` Gateway and Management hosts, builds and tests together under SDK 10.
	- [x] The expanded 12-project solution builds under SDK 10 with 0 warnings and 0 errors; 147 unit and 17 integration tests pass, including both host health checks and the no-route check.
	- [x] The existing Docker E2E run reports 8/8 passed in [the TRX results](../../TestResults/e2e/e2e-tests.trx). This verifies the pre-gateway E2E topology, not the new Gateway/Management images or Compose topology.
- [x] Verify prototype YARP, OIDC/JWT, PostgreSQL/EF/Npgsql, and credential-cryptography package/code compatibility for `net10.0`; see Gates 0.2 and 0.3 (the spikes also built a `net8.0` leg, now historical). This does not select final package versions or verify Redis integration/runtime behavior.
- [x] Prototype the framework-light credential contract/verification abstraction and PostgreSQL data adapters sharing one schema. The isolated spikes compiled and read the same row on both `net8.0` and `net10.0`; because every existing project is now `net10.0`, multi-targeting is unnecessary and the shared code can be ordinary `net10.0`. Production project/package choices and runtime integrations remain open.
- [x] Inventory existing `X-API-Key` callers: API middleware unit tests, API host/integration tests, E2E smoke tests, `ApiHostTestFactory`, and the manual HTTP request file. Source matches were inspected; generated `bin/`/coverage matches are not treated as source callers.
- [x] Owner decision: retain existing direct API tests as backend-test paths and add gateway-backed E2E coverage when that slice is implemented.

#### Gate 0.0 — .NET 10 upgrade of the existing solution (done)

All existing projects were upgraded from `net8.0` to `net10.0` and verified:

- [x] `global.json` pins SDK 10.0.301 (`latestPatch`); `Directory.Build.props` defaults to `net10.0`; `TestReportGenerator` explicitly targets `net10.0`; `.vscode/launch.json` uses `net10.0` paths; CI and integration workflows use `10.0.x`; Dockerfiles use `sdk:10.0.301` and `aspnet:10.0`.
- [x] Central packages moved to .NET 10 lines: ASP.NET Core OpenApi, Mvc.Testing, and Hosting.Abstractions 10.0.14; Http.Resilience 10.11.0; Logging.Abstractions 10.0.14; Swashbuckle.AspNetCore 10.3.1 (6.6.2 threw `TypeLoadException` at startup on .NET 10); OpenTelemetry packages 1.20.1 (Azure Monitor 1.6.0), which cleared the `OpenTelemetry.Api` NU1902 advisory.
- [x] Source and test fixes: `RedisIdempotencyStore` calls `JsonSerializer.Deserialize` with `responseJson.ToString()` to resolve a .NET 10 string/`ReadOnlySpan<byte>` overload ambiguity; `ApiEntryPoint` is a public marker in the API assembly that `ApiHostTestFactory` uses as `WebApplicationFactory<ApiEntryPoint>`, because the test project also references the Mock assembly and both expose a global `Program` (the marker must live in the API assembly so the factory finds the API's entry point); the partner-verifier timeout tests assert the effective `HttpStandardResilienceOptions.TotalRequestTimeout`, because the .NET 10 standard resilience handler leaves `HttpClient.Timeout` infinite; the redundant `client.Timeout` assignment was removed.
- [x] Solution hygiene: stale references to deleted `/tmp` prototype projects were removed, and `Release|Any CPU` mappings were added for every existing project, which removed the `MSB4121` warnings.
- [x] Verification: restore, Release solution build (0 warnings, 0 errors), 147 unit and 14 integration tests passing, and `dotnet format --verify-no-changes` exiting 0. The vulnerability scan reports no vulnerable packages.
- [ ] Deferred to final Phase 1 verification: a GitHub Actions run and Gateway/Management Docker image builds. Existing Docker E2E evidence is 8/8 passed; it predates the gateway.

#### Gate 0.1 — SDK/toolchain compatibility audit (read-only; historical, pre-upgrade)

Historical record: this audit ran before the upgrade, when the solution was on `net8.0` and `global.json` pinned SDK 8. Its findings no longer describe the current repository; see Gate 0.0.

Approved scope: inspect the current SDK/project/CI/Docker build setup and test whether the installed .NET 10 SDK can build existing `net8.0` projects. No implementation, project, workflow, Docker, or SDK configuration files were changed.

- [x] Confirmed local SDKs: .NET 8.0.422 (selected by current `global.json`) and .NET 10.0.301; the .NET 10.0.9 runtime is installed. CI remains pinned to .NET 8 and is not checked off as compatible.
- [x] Built the existing API, Mock, and test projects with both SDK 8 and SDK 10 from Release configuration; all succeeded with zero warnings and zero errors.
- [x] Ran the existing unit suite under SDK 10: 147 passed, 0 failed, 0 skipped. The test target remained `net8.0`, using the installed .NET 8 runtime.
- [ ] Confirmed that the planned new `net10.0` Gateway/Management hosts build with the solution — not verifiable because those projects do not exist. A temporary minimal GatewayHost compiled separately and is not a solution integration result.
- [x] Validated a minimal .NET 10 YARP/OIDC/EF Core/Npgsql host package graph using Yarp.ReverseProxy 2.3.0, Microsoft.AspNetCore.Authentication.JwtBearer 10.0.14, Microsoft.EntityFrameworkCore.Design 10.0.14, and Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; see Gate 0.3 below. This does not validate production routes, Keycloak runtime behavior, or Redis.

**Audit finding (pre-upgrade, historical):** `global.json` currently selects SDK 8.0.100 with `latestMinor` roll-forward, resolving to 8.0.422 in this environment. `Directory.Build.props` defaults projects to `net8.0`; the current apps/tests inherit that target, and TestReportGenerator explicitly targets `net8.0`. The installed SDK 10 builds the existing `net8.0` API, Mock, and tests successfully when invoked by absolute path from outside the repository's `global.json` scope. The solution-level Release build is not a conclusive all-project check: it emits `MSB4121` warnings because project Release configurations are missing from the solution mapping, and builds only the explicitly mapped tool project. Build individual projects or correct the solution mappings in an approved implementation slice.

#### Gate 0.2 — Cross-target contract and data-adapter spike (approved, isolated; historical multi-target evidence)

The `net8.0` leg of this spike is historical: no `net8.0` project remains, so a multi-target contract is unnecessary. The compile, migration, and read evidence for `net10.0` still stands.

This approved compatibility spike was performed under `/tmp/transactionvalidation-gateway-g0-spike`; it did not modify repository implementation, project, CI, Docker, SDK, or package configuration.

- [x] Proved a small framework-light credential/store/verifier contract targeting `netstandard2.1` builds and is consumable by both `net8.0` and `net10.0` projects with the .NET 10 SDK. The prototype explicitly set C# 10 because the `netstandard2.1` default language version rejected generated global usings/file-scoped namespace; after setting `LangVersion` to `10.0`, both builds passed.
- [x] Compiled distinct Npgsql data adapters targeting `net8.0` and `net10.0` against the same `gateway_backend_credentials` table shape, using parameterized SQL and Npgsql 10.0.3. Both restored and built successfully.
- [x] Inspected the restored Npgsql 10.0.3 package metadata: it provides dependency groups for `net8.0`, `net9.0`, and `net10.0`; its `net8.0` group resolved the .NET 8 abstractions, and its `net10.0` group resolved the .NET 10 abstractions.
- [x] Ran verifier behavior checks for valid credentials, wrong secrets, unknown IDs, revoked records, and unavailable pepper versions; all five expected outcomes passed.
- [x] Applied the generated EF migration to an isolated local PostgreSQL 15.17 cluster under `/tmp`; verified the created table, primary key, defaults, and check constraints with `psql`.
- [x] Inserted a non-sensitive test verifier row and read the same row through both the API `net8.0` and Gateway `net10.0` Npgsql adapters. Both returned bytea `010203FF` and a UTC timestamp (`DateTimeKind.Utc`).
- [x] Validated a temporary `net10.0` GatewayHost package graph with YARP, JWT bearer, EF Core, and Npgsql; see Gate 0.3. This was not the planned Gateway/Management product hosts.
- [ ] Validated the actual planned Gateway/Management hosts and complete dependency graph including Redis; those product projects do not exist yet.
- [ ] Selected final production package versions, project names, migration ownership, or a production shared persistence project; the spike is evidence, not that decision.

**Conclusion:** the preferred framework-light contract plus separate API `net8.0` and Gateway/Management `net10.0` Npgsql adapters sharing a schema passed isolated compile/package checks and a live PostgreSQL migration/read proof. Keep final project/package boundaries provisional pending review and integration into the real projects. The spike does not approve production API-auth changes or gateway implementation.

#### Gate 0.3 — YARP/OIDC/EF package and migration-model spike (approved, isolated)

This approved package/migration spike was performed in the same `/tmp/transactionvalidation-gateway-g0-spike` directory; it did not modify repository implementation, project, CI, Docker, SDK, or package configuration.

- [x] Confirmed NuGet latest stable versions at audit time: `Yarp.ReverseProxy` 2.3.0, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, and `Microsoft.EntityFrameworkCore.Design` 10.0.14. These are spike findings, not final pinned production versions.
- [x] Restored and built a `net10.0` minimal YARP/OIDC/EF Core/Npgsql host with the selected package set; build succeeded with no reported warnings or errors.
- [x] Added typed OIDC options with startup data-annotation validation in the prototype host; the host compiled successfully.
- [x] Generated an EF Core 10.0.14 migration from the credential model. The migration contains the shared credential table, binary verifier, pepper version, validity timestamps, primary key, default creation timestamp, and check constraints; generated model columns/checks match the table contract used by both adapters.
- [x] Ran `dotnet ef migrations list --no-connect`; EF discovered the generated migration without requiring a live PostgreSQL server.
- [x] Applied the migration to PostgreSQL and verified runtime reads from both target adapters against the same database; this used a disposable native PostgreSQL cluster because Docker daemon was unavailable.
- [ ] Validated YARP route/proxy behavior; no route runtime test was run.
- [ ] Validated OIDC token issuance/validation against Keycloak; only package restore and host compilation were checked.

**Conclusion:** the selected .NET 10 package graph and EF model/migration generation are viable at compile-time; the prototype migration and shared schema also passed a live PostgreSQL 15.17 apply/read check from both target adapters. Package recency and a minimal database proof do not establish production package approval, full schema suitability, YARP runtime correctness, OIDC security correctness, or Redis behavior. The schema/model is still a prototype and must be reviewed before adoption.

#### Gate 0.4 — Redis atomic rate/quota spike (partial, isolated)

- [x] Started Redis 7 in a Docker container published only on `127.0.0.1:56379`; `redis-cli ping` returned `PONG`. The temporary container has been removed.
- [x] Concurrent quota-cap assertion passed: 31 of 128 requests allowed, 97 rejected, daily/monthly counters both ended at 31.
- [x] Concurrent shared-bucket assertion passed: 7 allowed and 73 rate-limited across simulated versions sharing the same client+API keys; daily/monthly usage ended at 80, confirming quota is charged before rate rejection.
- [ ] Finite `Retry-After` validation did not pass. The test configured a zero refill rate while asserting a finite retry estimate; the harness stopped at that assertion. The user reverted the temporary Lua/test files, so no corrected rerun has occurred.
- [ ] Redis outage/fail-closed behavior, the production Redis client package integration, and the final policy script against the actual gateway are not validated by this spike.

**Result:** atomic counter ordering/caps received partial evidence, but Gate 0.4 is not passed because retry timing and fail-closed behavior remain unchecked.

**Gate 0 status:** the .NET 10 upgrade, isolated package/contract spikes, and live PostgreSQL migration/read have evidence. The owner confirmed Docker/.NET 10 compatibility and chose the test strategy; the existing Docker E2E report is 8/8. The 12-project solution now builds, including Gateway/Management health-only skeletons. A CI run and Gateway/Management image builds are intentionally deferred to final Phase 1 verification, not prerequisites for implementation slices. YARP route runtime, Keycloak OIDC runtime, Redis package integration/outage handling, and finite-refill retry-delay behavior remain open feature validations.

**Stop condition:** if shared PostgreSQL credential validation cannot be implemented safely with the proposed shared contract and data adapters, present alternatives and get approval before changing API authentication or persistence architecture.

### Slice 1 — Project/build skeleton

- [x] Add Gateway and Management `net10.0` host skeletons with `/healthz`; YARP was initially registered with an empty configuration section. Slice 3 adds the configured public route.
- [x] Add the `Gateway.Contracts` and `Gateway.PolicyStore` library project boundaries after Gate 0 package/target compatibility checks. Their feature contracts and persistence behavior remain for later slices.
- [x] Add all four projects to the solution and add Gateway/Management Dockerfiles. Update existing API/Mock Dockerfiles to include the new solution project files in their pre-restore layers.
- [x] Add the verified YARP package version to central package management and reference it only from the Gateway host.
- [x] Add project references for the shared contract/store boundaries; verify Gateway and Management do not reference the executable API.
- [x] Add host health integration tests in the existing test project and VS Code launch profiles. Existing CI solution build and test commands include the new projects/tests without a workflow change.

**Verification tasks:**

- [x] Build the whole 12-project solution under SDK 10: 0 warnings, 0 errors; solution project graph includes all four additions.
- [x] Run all 147 unit tests and 17 integration tests, including Gateway/Management health startup and the no-route check; verify formatting.
- [x] Keep API and Gateway/Management host tests in the existing SDK 10 test project; CI filters unit tests and the integration workflow selects `Category=Integration`.
- GitHub Actions runs and Docker image builds are deferred to the final Phase 1 verification step below; they are not Slice 1 exit criteria.

### Slice 2 — Shared security contracts and backend authentication

**Approved scope:** define shared gateway-auth header/client-identity contracts and a credential-verifier interface; add an API backend-authentication mode that validates a configured gateway credential and a syntactically valid client ID; retain direct API-key authentication only through explicit `DirectApiKey` configuration in Development for local E2E and direct API tests. This slice does not add PostgreSQL-backed credential records, peppered verifiers, credential rotation/expiry, or gateway client-key authentication; those remain in later slices.

- [x] Define framework-light gateway-auth header names, an opaque authenticated client-ID type, and the gateway credential verifier interface.
- [x] Add backend authentication middleware/options in the shared Configuration layer. Gateway mode requires `X-Gateway-Key` and `X-Authenticated-Client-Id`; credential comparison is fixed-time.
- [x] Make Gateway authentication the default API mode and require its credential configuration at host startup. Restrict explicit direct API-key mode to Development and configure it for local E2E and API test hosts.
- [x] Exempt only `/healthz` from backend authentication; the existing platform health probe remains available.
- [x] Preserve existing direct API tests through explicit test-host configuration.

**Verification tasks:**

- [x] Test valid, invalid, and missing gateway credentials, malformed/missing client IDs, direct API test mode, and unauthenticated health checks.
- [x] Verify rejected gateway authentication causes no transaction publication; valid gateway auth reaches the existing transaction path.
- [x] Verify Release build (0 warnings/errors), formatting, and all 170 unit/integration tests. Re-run `test:e2e`: API/Mock images build and the existing direct-API Compose topology passes 8/8; this does not exercise Gateway/Management.
- [ ] Test persisted credentials, expiry, rotation overlap, and key revocation when credential persistence is implemented in a later slice.

### Slice 3 — YARP route and transparent proxy behavior

- [x] Add stable route ID `transactionValidationV1`.
- [x] Match `POST /transactions/v1/partner/transactions`; rewrite to `/api/v1/partner/transactions`.
- [x] Configure the backend destination as `http://api:8080/` through YARP configuration, allowing deployment/test overrides without environment-specific code.
- [x] Use YARP's streaming proxy behavior; preserve query, request body, backend status/body/headers; do not add retries, response normalization, or gateway idempotency.
- [x] Map connection failures to sanitized `502` and upstream activity timeouts to sanitized `504`; do not expose backend addresses or exception details.
- [x] Apply an explicit POST method allowlist; other methods on the public transaction path return `405` with `Allow: POST`.

**Verification tasks:**

- [x] Test route match/rewrite, configured destination, query preservation, and request-body pass-through using a local test backend.
- [x] Test allowed/disallowed methods and backend status/body/header pass-through.
- [x] Test client cancellation, backend connection failure, and upstream activity timeout.
- [x] Verify a successful proxy request invokes the backend once and cancellation does not trigger a retry.
- [ ] Gateway-backed API E2E remains pending until the Compose path exercises the client authentication and trusted backend-header chain.

### Slice 4 — Client authentication and policy authorization

- Depends on Slice 4A credential persistence and secret-provider foundations.
- [x] Implement key format `Authorization: ApiKey <opaque-key-id>.<secret>`; key IDs are unique and never reused. The secret wire representation is 32 cryptographically random bytes encoded as unpadded Base64URL.
- [x] Verify stored HMAC-SHA-256 verifier with constant-time comparison and versioned pepper retrieved from secret provider.
- [x] Enforce current/previous key states, expiry, one-hour overlap, and client disable.
- [x] Apply generic `401` for all invalid credentials before route lookup; after authentication return `404` for unknown API/version and `403` for disallowed API/version.
- [x] Enforce the configured client API/version allowlist and POST method. Sanitize incoming auth, identity, correlation, and forwarding headers; set trusted `X-Authenticated-Client-Id`, backend `X-Gateway-Key`, and canonical correlation ID.

**Verification tasks:**

- [x] Test credential parsing, malformed values, unknown IDs, and wrong verifiers.
- [x] Test active/expired/revoked keys, rotation overlap, and disabled clients.
- [x] Verify invalid credential cases produce equivalent generic `401` responses.
- [x] Test route permissions and spoofed-header removal.
- [x] Verify the backend never receives the client secret.

**Status:** Gateway authentication, authorization, trusted-header handling, and targeted host/proxy tests are implemented. The end-to-end Docker path and human governance review remain pending; Slice 4 is not a governance certification.

### Slice 4A — Credential persistence and local secret-provider foundations

**Approved scope:** provide the minimum shared persistence and local secret-provider primitives required by Slice 4. This is a prerequisite foundation only: it does not authenticate partner requests, issue/manage credentials, implement management endpoints/OIDC/auditing, add Compose services, or change Gateway/API runtime behavior.

- [x] Add framework-light contracts for client-key records, API/version grants, a credential-store lookup, and a secret provider.
- [x] Add PostgreSQL EF Core entities/context and a versioned migration for clients, client-key verifier metadata, and API/version grants. Store only HMAC-SHA-256 verifier bytes and pepper version; never raw client secrets or pepper values.
- [x] Implement parameterized EF-backed credential lookup by opaque key ID, returning client enabled state and grants needed by Slice 4. No management writes or policy cache in this slice.
- [x] Implement a local file secret-provider adapter for versioned peppers. Constrain secret names to safe file names under the configured `.secrets/` root, reject symbolic links and unsafe Unix permissions, fail explicitly for missing/invalid files, and never log secret contents.
- [x] Ignore `.secrets/` in source control; do not add sample pepper values or generated credentials.

**Verification tasks:**

- [x] Verify EF model/migration schema, verifier/status/revocation/validity constraints, and lookup indexes; a PostgreSQL trigger prohibits key-row deletion or key-ID changes so IDs cannot be reused.
- [x] Test migration up and rollback plus repository lookup against PostgreSQL in an isolated temporary schema, including missing keys, disabled clients, invalid revocation state, prohibited key deletion, and prohibited key-ID changes; verify no raw-secret field exists in persisted entities/schema. Set `GATEWAY_TEST_POSTGRES` to run the opt-in PostgreSQL test; it creates and drops a uniquely named schema.
- [x] Test local secret reads, safe-name/path traversal and symbolic-link rejection, missing files, and file/directory permission handling; verify Windows fails closed because ACL validation is not implemented.
- [x] Build, format, and run targeted tests. Docker image builds and CI remain deferred to final Phase 1 verification.

**Status:** Slice 4A implementation and technical checks are complete; governance completion is pending human review of the diff and applicable security/architecture evidence. Explicit bounded PostgreSQL connection/command timeouts remain a required host-composition gate before runtime registration. This status is not a governance certification.

### Slice 5 — Management API, PostgreSQL, OIDC, and audit

- [ ] Extend the Slice 4A schema with explicit limits, backend credential verifiers/rotation, route kill switch, optimistic concurrency, and audit entries.
- [ ] Add private management host and OIDC bearer validation configured by issuer/audience; require read/write scopes.
- [ ] Configure Keycloak import for local realm, client, scopes, and test identities. Keep development credentials in `.secrets/`.
- [ ] Implement client/key issue, rotate, revoke, disable, permissions, quota policy metadata, backend credential metadata, route kill switch, and audited change endpoints.
- [ ] Show generated client/backend secret only once; never include secrets in read responses, logs, or audit entries.
- [ ] Implement append-only audit for admin changes and auth failures; retain stable OIDC `iss`+`sub` and admin client ID, action, target, time, outcome.
- [ ] Enforce optimistic concurrency and fail closed with `503` on PostgreSQL outage.
- [ ] Create a safe local bootstrap process for initial admin and client; do not embed production admin users, keys, or passwords in the repo.

**Verification tasks:**

- [ ] Test OIDC issuer/audience/scope failures and unauthorized/forbidden results.
- [ ] Test admin CRUD and concurrency conflicts.
- [ ] Verify one-time secret behavior, non-retrievability, and secret-free audit/logging.
- [ ] Test key-ID uniqueness/no reuse, disabled client, and append-only audit behavior.
- [ ] Test fail-closed behavior during PostgreSQL outage.

### Slice 6 — Local TLS, secret provider, and Compose profiles

- [x] Implement the provider-neutral `ISecretProvider` contract and local `.secrets/` file adapter in Slice 4A; define later AWS Secrets Manager and Azure Key Vault adapters as separate deployment slices.
- [ ] Add local CA/certificate generation script and explicit trust setup. Keep cert/key files ignored and protect file permissions.
- [ ] Configure HTTPS client→local ingress→gateway, gateway→API, and TLS PostgreSQL/Redis connections. Correctly configure database TLS rather than treating database protocols as HTTPS.
- [ ] Full Compose default includes Gateway, Management API, API, Mock, RabbitMQ, PostgreSQL, Redis, and Keycloak. Keep API/Mock/data services internal; expose gateway only.
- [ ] Add an infrastructure-only Compose profile for IDE use, including dependencies the API still needs (Mock/RabbitMQ/Redis/PostgreSQL/Keycloak/TLS as applicable).
- [ ] Add startup ordering/readiness and health checks without treating `depends_on` alone as service readiness.
- [ ] Seed a local-only TransactionValidation v1 client with explicit finite but generous limits. Store its one-time secret in ignored `.secrets/`; never apply the seed to cloud deployments.

**Verification tasks:**

- [ ] Validate Compose configuration and clean startup/shutdown.
- [ ] Test certificate trust, host access, and readiness failures.
- [ ] Verify ignored-secret configuration and local secret/bootstrap reproducibility.

### Slice 7 — Minimum local quota/rate enforcement

Implement the approved minimum Redis-backed token bucket and daily/monthly UTC quota path in Phase 1 so the local seeded client is usable and subject to its explicit finite limits. Phase 2 generalizes and hardens policy enforcement across APIs and operational scenarios; it does not introduce the first enforcement path.

- [ ] Implement the minimum rate and quota check as one atomic Redis decision/update; define and test concurrency and counter-expiry behavior.
- [ ] Require explicit finite settings per client/API, including the local seed client; no unlimited defaults or bypass configuration.

**Verification tasks:**

- [ ] Verify counters are per client+API and shared across versions; quota is charged after authentication/authorization and before rate-limit evaluation/forwarding.
- [ ] Test `429` responses and appropriate `Retry-After` values for token-bucket and UTC quota exhaustion.
- [ ] Test fail-closed `503` behavior on Redis failure.
- [ ] Verify local seed limits are generous but finite and no integration test bypasses enforcement.

### Slice 8 — Observability, errors, and readiness

- [ ] Add metadata-only structured access/security logs; include stable route ID, API/version, client/key ID where safe, source IP, status, latency, decision, and canonical correlation ID. Omit query values and bodies; never log credentials.
- [ ] For unparseable credentials, log outcome, source IP, correlation ID, and route ID only.
- [ ] Return canonical correlation ID in `Correlation-Id`; validate/generate inbound ID and overwrite client-supplied trusted headers.
- [ ] Add a consistent JSON schema for gateway-generated errors; preserve backend responses. Implement agreed status mapping: `401`, `403`, `404`, `405`, `410`, `413`, `429`, sanitized `502`/`504`, and `503` store/policy failures.
- [ ] Return the canonical correlation ID in `Correlation-Id`; validate/generate inbound IDs and overwrite client-supplied trusted headers.
- [ ] Add liveness and readiness separate endpoints exempt from client auth. Readiness reflects request capability: fresh policy cache and Redis availability; fresh PostgreSQL outage cache is usable only until TTL.
- [ ] Add network-restricted Prometheus endpoint and low-cardinality aggregate metrics; never use client/key IDs as metric labels.

**Verification tasks:**

- [ ] Test response schema/status and correlation ID propagation.
- [ ] Verify log redaction and metric label cardinality.
- [ ] Test health behavior during each dependency outage and probes without client credentials.

### Final Phase 1 verification — run after all implementation slices

These are end-of-phase checks, not prerequisites for beginning or completing individual implementation slices. Continue to run each slice's targeted local build, format, and test checks as part of that slice.

- [ ] Build all Phase 1 Docker images, including Gateway, Management, API, and Mock, using the configured .NET 10 toolchain.
- [ ] Run the applicable GitHub Actions build, unit, integration, and E2E workflows; review their results.
- [ ] Run the completed full Compose topology and gateway-backed E2E checks against the final images.

## Phase 1 governance applicability

- **Applicable:** `CS-HOST-01`, `CS-EXT-01`, `CS-CFG-01`, `CS-CFG-02`, `CS-HTTP-01`, `CS-OBS-01`, `CS-TEST-01`, `CS-TEST-02`, `CS-TEST-03`, and `CS-TEST-04` where their stated conditions apply; `CS-RES-01` for bounded timeout/cancellation only (gateway retries are prohibited); repository architecture rules for boundaries; relevant `RCFG-BASELINE-01`, `RCFG-DEPENDENCY-01`, `RCFG-COMPOSE-01`, `RCFG-STYLE-01`, `RCFG-WORKFLOW-01`, and `RCFG-VSCODE-01`.
- **Conditional:** `CS-FLOW-01` for management state transitions and credential issuance/revocation; `CS-VAL-01` only for finite parsed input (do not model opaque IDs as enums); `RCFG-COVER-UNIT-01`, `RCFG-COVER-INT-01`, and `RCFG-CODECOV-01` if new test projects/artifacts change coverage reporting.
- **Not applicable to Gateway/Management:** `CS-MSG-01` because these services do not publish/consume broker messages; `CS-DEBUG-01` unless committed debug profiles are added. Existing backend message flow remains in place and still follows its applicable rules.
- **Deferred:** `TXV-DEL-01/02/03` and cloud-specific Azure/AWS infrastructure rules are not Phase 1 implementation requirements.

Before coding:

- [ ] Record a requirement-to-rule matrix for Phase 1; every rule ID must be marked applicable with an implementation/test mapping or N/A with reason.

## Governance enforcement during implementation

The checklist is a progress record, not proof of compliance by itself. Follow the ordered [AI-assisted development standard](../governance/ai-assisted-development.md) and [feature delivery workflow](../governance/feature_delivery_workflow.md) for every implementation slice. A slice is not complete until its applicable rules have mapped evidence and a human reviewer has inspected the change.

### Mandatory slice workflow

For each slice, before editing:

- [ ] Restate its observable outcomes, scope, non-goals, dependencies, and failure cases.
- [ ] Inspect current source, tests, configuration, build workflows, and the owning feature/operations documents.
- [ ] Review the governance index and the authorities selected for this slice; record each applicable stable Rule ID and title, the planned implementation pattern, and its verification.
- [ ] Mark reviewed but inapplicable rules N/A with a slice-specific reason. Do not waive an applicable MUST/MUST NOT rule by labeling it N/A.
- [ ] Identify ownership, project dependencies, contracts, trust boundaries, configuration/secrets, side effects, and success/failure invariants.
- [ ] Stop for owner/security/architecture clarification if a requirement, mandatory rule, or cross-slice contract conflicts or is materially ambiguous.

For each slice, after editing:

- [ ] Add or update focused tests for observable behavior and important failure/invariant cases at the owning boundary.
- [ ] Run the targeted test(s), build/analyzers, and format check required by that change; run integration or Compose E2E where real host/network/store behavior is part of the acceptance criterion.
- [ ] Inspect the complete diff for secrets, accidental bypasses, unrelated changes, project-boundary violations, and consistency with the approved design.
- [ ] Update the rule matrix with actual evidence (test name/result, command, workflow/check, or review finding); do not claim a check passed unless it was run.
- [ ] Update the owning feature documentation and architecture/operations docs only where their owned behavior changed; link implementation claims to source, tests, configuration, or workflow evidence.
- [ ] Obtain human review of the diff and applicable security/architecture review before marking the slice complete.

### Rule-to-evidence register

Maintain this register in the implementation/review record, with one row per applicable Rule ID and per-slice evidence. This table is a minimum; inspect the rule authority for the exact MUST/MUST NOT text and add rules whenever the slice introduces the corresponding concern.

| Rule / authority | Apply to | Required evidence before completion |
|---|---|---|
| Repository architecture rules | Project creation, dependency references, API/backend boundary, data ownership, Compose network exposure | Project-reference review; host/service ownership review; tests/configuration showing the backend is not a public partner entry point |
| `CS-HOST-01` | Gateway, Management, and any changed executable host | Composition-root review; behavior is in owning services/adapters rather than `Program` |
| `CS-EXT-01` | YARP, PostgreSQL, Redis, OIDC, secret-provider, and backend adapters | Contract/adapter tests for protocol and outcome mapping; host registration tests |
| `CS-CFG-01` | Route, authentication, timeout/body, limit, OIDC, TLS, database, and Redis settings | Typed binding and startup/policy validation; tests for missing, malformed, and boundary values |
| `CS-CFG-02` | Client/backend secrets, peppers, OIDC credentials, TLS private keys, local bootstrap | Secret-source/rotation review, ignore/secret scan, and log/audit redaction tests |
| `CS-RES-01` | HTTP and data-store calls, proxy timeouts, cancellation | Bounded timeout and cancellation tests; explicit verification that prohibited gateway retries are absent |
| `CS-FLOW-01` | Client/key issue/rotate/revoke/disable, policy changes, limit charge, and request forwarding | State-transition tests proving authorization/validation precede side effects and counters follow the approved charge order |
| `CS-HTTP-01` | Gateway/management-generated errors and proxy failures | Status/content-type/safe-detail tests; backend pass-through tests remain separate |
| `CS-OBS-01` | Correlation, logs, audit, health, and metrics | Correlation tests; secret/body/query redaction review; low-cardinality label tests; dependency-aware readiness tests |
| `CS-TEST-01` and `CS-TEST-02` | Every changed behavior and failure path | Boundary-appropriate tests with deterministic, narrowly scoped failure controls isolated from production |
| `CS-TEST-03` | Middleware, host registration, Gateway/Management/API integration | Tests exercise the real host/pipeline and replace only selected external dependencies |
| `CS-TEST-04`, `RCFG-COMPOSE-01` | Full local multi-service gateway/backend/store contract | Compose validation and relevant E2E through real service networking and dependencies; document any approved exception |
| `CS-VAL-01` | Finite external values such as policy enums, headers, identifiers with defined syntax | Parser/validation tests; opaque IDs remain opaque and are not modeled as enums |
| `RCFG-BASELINE-01`, `RCFG-DEPENDENCY-01` | SDK, shared build settings, central packages, project references | SDK selection, restore/build, package-version centralization, and separated package/project reference groups |
| `RCFG-STYLE-01` | C# style/analyzer and format changes | `dotnet build` for analyzers plus separate format-verification task |
| `RCFG-WORKFLOW-01`, `RCFG-VSCODE-01` | CI/integration workflows and shared tasks/settings/launch profiles | Least-privilege workflow review; task/CI command parity; portable paths and no secrets |
| Coverage rules (`RCFG-COVER-UNIT-01`, `RCFG-COVER-INT-01`, `RCFG-CODECOV-01`) | Only if test projects or coverage artifact/filter/report configuration changes | Unit/integration separation, correct collection paths, and CI upload/config alignment |
| `CS-MSG-01` | N/A for Gateway/Management if they do not publish/consume messages | Record that these hosts own no broker messaging; backend messaging changes remain subject to the rule |
| `CS-DEBUG-01` | Only if shared launch/debug profiles are added or changed | Validate workspace-relative paths, Development-only non-secret settings, and secret absence; otherwise record N/A |
| `TXV-DEL-01/02/03` | N/A for local-only Phase 1; re-evaluate if delivery workflows or Azure deployment are changed | Record N/A for Phase 1; apply repository-specific workflow evidence if a later slice changes Azure delivery |

### Completion and escalation controls

- [ ] Do not change a work/verification checkbox to `[x]` until its acceptance condition is demonstrated and the evidence is recorded beside the item or in the rule register.
- [ ] Do not merge a slice with failed mandatory checks, an unexplained rule mapping, an unresolved security/data-integrity conflict, or an undocumented deviation.
- [ ] If a check is unavailable, leave the checkbox unchecked and record the blocker, impact, owner, and required follow-up; do not substitute an assertion that the change is compliant.
- [ ] If an applicable rule cannot be followed, use the feature-delivery exception record (Rule ID/title, reason, scope/impact, compensating controls, approver, review/expiry condition, verification). Security, privacy, and data-integrity requirements cannot be waived informally.
- [ ] Require a human reviewer to inspect the final diff and evidence; AI-generated implementation and this checklist do not certify correctness or security.

## Pre-implementation design gates and approval request

- [x] **.NET compatibility validation:** verify the .NET 10 SDK/configuration and that the new `net10.0` hosts build with the existing solution; verify YARP/OIDC/database package compatibility. Run CI and Docker image builds at final Phase 1 verification.
- [ ] **Backend credential integration validation:** choose the shared credential contract and PostgreSQL data-adapter structure as `net10.0` projects, informed by the Gate 0.2 and 0.3 spikes. If the design fails, present alternatives before changing auth semantics or schema ownership.
- [ ] **Compose topology gate:** preserve API dependencies on Mock and RabbitMQ; add PostgreSQL, Redis TLS, Keycloak, gateway, management API, and local TLS endpoint support. Confirm service names and IDE host reachability.
- [ ] **Security gate:** review backend key verifier/pepper storage, first-admin/client bootstrap, local secret handling, OIDC scope mapping, health-check exposure, and private network access before implementation.
- [x] **Owner approval for Slice 1 only:** recorded in the owner’s 2026-11-09 response; remaining feature slices require their own scope/design review before implementation.

**Approval requested:** approve this Phase 1 proposal and any remaining Compose, local TLS, bootstrap, and worktree-scope choices before feature implementation. The .NET 10 upgrade of the existing solution is complete and minimum Phase 1 Redis rate/quota enforcement is a confirmed decision. Begin with compatibility validation and pause if it invalidates the selected design.

## Related documents

- [Full YARP gateway implementation plan](yarp-gateway-implementation-plan.md)
- [YARP gateway clarification decisions](yarp-gateway-clarification-decisions.md)
- [Project governance setup checklist](../governance/project_governance_bootstrap.md)
- [AI-assisted development standard](../governance/ai-assisted-development.md)
- [Feature delivery workflow](../governance/feature_delivery_workflow.md)
