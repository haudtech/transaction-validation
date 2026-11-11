# YARP Gateway Phase 1 — Code Implementation Proposal

## Status and approval boundary

**Slices 1–3 and 4A are implemented.** On 2026-11-09 the owner approved the proposed project names, direct Gateway HTTPS as the local TLS boundary, versioned Keycloak plus local-only bootstrap, retaining direct API tests while adding gateway-backed E2E coverage, and the narrow Slice 2 scope. The owner subsequently approved Slices 3 and 4A. Slice 4 and subsequent work remain gated.

This document breaks Phase 1 of the [YARP Gateway Implementation Plan](yarp-gateway-implementation-plan.md) into owned code slices, dependencies, tests, and approval gates. The .NET 10 upgrade of the existing solution is complete, and minimum Phase 1 Redis enforcement is a confirmed decision. Gateway client authentication, credential lifecycle behavior, management behavior, and Docker Compose topology remain unimplemented.

## Request and acceptance criteria

Build a local Docker-first YARP gateway and a separate private management API in this solution. Route a partner request through the gateway to the existing TransactionValidation API while retaining the API’s existing business validation, idempotency, partner verification, and message publishing responsibilities.

Phase 1 is accepted when:

- The existing solution is on .NET 10 (done): `global.json` pins SDK 10.0.301, every project targets `net10.0`, CI workflows use `10.0.x`, and the Dockerfiles use `sdk:10.0.301` and `aspnet:10.0`. The new Gateway/Management hosts also target `net10.0`.
- The backend credential contract/verification abstraction and the PostgreSQL schema are shared by the API, Gateway, and Management as ordinary `net10.0` code; no multi-targeting is required.
- Full Docker Compose starts the gateway, management API, TransactionValidation API, Mock, RabbitMQ, PostgreSQL, Redis-compatible store, and Keycloak; an infrastructure-only profile supports IDE work.
- Public `POST /transactions/v1/partner/transactions` rewrites to backend `POST /api/v1/partner/transactions`.
- The gateway authenticates a client key, authorizes access to TransactionValidation v1, strips client credentials, and sends only the trusted client ID and separate gateway credential to the backend.
- The backend validates the gateway credential and trusted client ID in the gateway-backed runtime; unauthorized traffic causes no transaction side effects.
- The private management API authenticates administrators with local Keycloak OIDC, scopes management operations, supports one-time key issuance/rotation/revocation, and records auditable changes without secrets.
- The local Compose client has explicit finite limits enforced atomically through Redis in Phase 1; Phase 2 generalizes and hardens the feature.
- Local HTTPS and local secret/certificate handling work without committing private material.
- Existing backend functionality and relevant API tests continue to pass through explicitly supported test/runtime modes.

## Scope boundaries

### In scope

- Separate YARP gateway/private management API hosts, targeted at `net10.0` (the existing solution's .NET 10 upgrade is already done).
- Provider-neutral policy, credential-verification, and secret-provider contracts.
- PostgreSQL policy/audit storage, migrations, Keycloak local realm setup, and minimum Redis-backed token-bucket plus daily/monthly UTC quota enforcement in Phase 1.
- Backend authentication contract change for gateway-backed requests.
- Full and infrastructure-only local Compose profiles, local TLS, test clients, tests, build/CI wiring, and directly affected documentation.

### Out of scope

- AWS/Azure deployment infrastructure and provider-specific secret-manager adapters.
- API Management, a gateway UI, mTLS, request signing, partner OAuth2/JWT, or user authorization.
- Cloud numeric sizing/limits, production DNS cutover, cross-cloud active-active, and AWS orchestration choice.
- Gateway retries, gateway-owned business idempotency, body buffering/transformation, WebSockets, or HTTP/3.

## Existing implementation facts and impact

- `global.json` pins .NET SDK 10.0.301 (`latestPatch`), `Directory.Build.props` defaults to `net10.0`, CI and integration workflows install `10.0.x`, and Dockerfiles use .NET 10 images. Earlier standalone image builds were OOM-killed; the latest `test:e2e` task built the API and Mock images successfully. Gateway/Management image builds and CI runs remain deferred to final Phase 1 verification.
- `TransactionValidation.Api` exposes `POST /api/v1/partner/transactions`. Its default Gateway authentication mode validates configured `X-Gateway-Key` and `X-Authenticated-Client-Id`; explicit `DirectApiKey` mode is restricted to Development for local E2E and direct API tests.
- `docker-compose.yml` currently runs API, Mock, RabbitMQ, and Redis. The API depends on all three supporting services for the current transaction path.
- The test project targets `net10.0`; its direct API integration tests explicitly select `DirectApiKey` mode and host the API through `WebApplicationFactory<ApiEntryPoint>`. `ApiEntryPoint` is a marker in the API assembly because the test project also references the Mock assembly, which exposes its own global `Program`.
- The Gateway has a configured TransactionValidation v1 route and Compose-DNS destination. `Gateway.PolicyStore` now has the initial client/key/grant schema, migration, and read-only lookup; `Gateway.Secrets` provides the local file adapter. Gateway client authentication, management API, Keycloak realm import, credential lifecycle, and local HTTPS CA setup remain unimplemented.

The implementation must not accidentally discard the existing backend dependencies or silently break direct API tests while switching the deployed/local gateway-backed path to gateway credentials.

## Proposed architecture and project ownership

| Proposed project/component | Target | Responsibility |
|---|---|---|
| `TransactionValidation.Gateway` | `net10.0` | YARP host; static route match/rewrite; client-key auth; API/version permission checks; trusted-header construction; gateway errors; correlation; health/logging/metrics; secret and policy interfaces. |
| `TransactionValidation.Gateway.Management` | `net10.0` | Private management endpoints; OIDC issuer/audience/scope validation; client/key/permission/quota/backend-credential operations; optimistic concurrency; audit trail. |
| `TransactionValidation.Gateway.Contracts` (name provisional) | `net10.0` | Framework-light DTOs/interfaces shared where needed; no ASP.NET Core host types. |
| `TransactionValidation.Gateway.PolicyStore` | `net10.0` | PostgreSQL policy schema and read-only credential lookup; management writes are deferred. |
| `TransactionValidation.Gateway.Secrets` | `net10.0` | Local `.secrets/` implementation of the provider-neutral secret contract; cloud adapters are deferred. |
| Existing `TransactionValidation.Api` | `net10.0` (already upgraded) | Transaction workflow and backend-side validation of gateway credential plus trusted client ID in gateway-backed mode. |

No Gateway or Management project may reference the executable API project. YARP route configuration is deployment configuration, not PostgreSQL policy. Client/API permissions, credential verifiers, quotas, route kill-switch state, and audit records belong in PostgreSQL. Rate/quota counters belong in Redis.

## Ordered code slices

### Gate 0 — Compatibility spike

Before adding production behavior:

1. Confirm SDK 10.0.301 and the `latestPatch` roll-forward are available in CI. The local pin, workflow versions, and Dockerfile base images are already on .NET 10, but no CI run or image build has been verified.
2. Prove the full solution, including the new `net10.0` hosts, restores, builds, tests, and formats under SDK 10. The existing solution already builds with 0 warnings and passes `dotnet format --verify-no-changes`.
3. Verify compatible stable YARP, OIDC, EF Core/Npgsql, Redis, and cryptography package versions for `net10.0`.
4. Choose the shared credential contract and PostgreSQL data-adapter structure as `net10.0` projects, informed by the isolated Gate 0 spikes. Validate it before committing to exact project/package boundaries.
5. Decide how existing tests and local direct API callers continue to work after gateway-backed authentication is introduced.

**Gate output:** a short compatibility result for the CI/Docker toolchain and shared contract/adapter arrangement before committing to exact project and package boundaries. If the arrangement fails, present alternatives before changing authentication semantics or PostgreSQL schema ownership. Phase 1 minimum Redis enforcement is a confirmed decision.

### Slice 1 — Solution/project/build skeleton

- Add Gateway and Management project skeletons, target `net10.0`, health endpoints, and clean startup.
- Add proposed contract/store projects after Gate 0 validates target and dependency compatibility.
- Add projects to the solution and centrally manage compatible package versions.
- Add the Gateway and Management projects to the solution, CI, and Docker, targeting `net10.0`. The `global.json` pin, existing project targets, CI workflow versions, Dockerfile base images, and VS Code debug paths are already on .NET 10.
- Choose test projects so API tests and Gateway/Management host tests all run under the same SDK 10 baseline.
- Add basic health endpoints but no public gateway route yet.

**Tests/checks:** restore and build the whole solution with SDK 10 (Release configurations are already mapped for every existing project); verify the package graph, host startup, `dotnet format --verify-no-changes`, CI invocation, and image builds with sufficient Docker memory.

### Slice 2 — Shared security contracts and API backend authentication

**Approved scope:** add framework-light gateway-auth header/client-identity contracts and a credential-verifier interface; add an API authentication mode that validates a configured gateway credential and a syntactically valid client ID; retain direct API-key auth only through explicit `DirectApiKey` configuration in Development for local E2E and direct API tests. This slice does not implement PostgreSQL credential records, peppered verifiers, expiry/rotation, or gateway client-key authentication; those remain in later slices.

- Define shared header constants, an opaque authenticated client-ID type, and the gateway credential verifier interface.
- Add backend middleware/options in the Configuration layer. Gateway mode requires `X-Gateway-Key` and `X-Authenticated-Client-Id`; compare credentials in fixed time.
- Make Gateway mode the default and require its configured secret at startup. Restrict direct API-key mode to Development and configure it explicitly for local E2E and direct API test hosts.
- Validate direct API-key mode is Development-only; validate that rejected Gateway-mode credentials never reach transaction publication.
- Exempt only `/healthz`; retain the existing platform probe.
- Verify rejected authentication stops the request before transaction side effects. Release build (0 warnings/errors), format verification, 170 unit/integration tests, and existing direct-API Docker E2E (8/8) pass; API/Mock Docker images built successfully.

**Tests:** valid/invalid/missing gateway credentials; missing/malformed client ID; direct API test mode; health probe; no transaction publishing on rejected auth; valid gateway auth reaches the existing transaction path. Persistence, expiry, rotation, and revocation tests belong to later slices.

### Slice 3 — YARP route and proxy behavior

- Add a stable route ID for TransactionValidation v1 and static route definition.
- Match `POST /transactions/v1/partner/transactions` and rewrite to `/api/v1/partner/transactions`.
- Configure backend destination through YARP configuration, using Compose DNS by default and allowing deployment/test overrides.
- Stream request/response bodies unchanged; preserve backend status/body and explicitly approved response headers.
- Do not retry, normalize backend errors, buffer bodies, or implement business idempotency.
- Map connection failures/activity timeouts to sanitized `502`/`504`; never expose exception text/backend host details.
- Allow only configured HTTP methods; unsupported method returns `405`.

**Tests:** route matching, rewrite, query/body preservation, HTTP methods, pass-through status/body/headers, cancellation, backend unavailable/activity timeout responses, and proof that proxying does not retry. Correlation and trusted-header handling are covered by later slices. Gateway-backed API E2E remains pending until trusted backend headers and Compose wiring are implemented.

### Slice 4 — Client key authentication and API/version authorization

Depends on Slice 4A's persisted client-key lookup and versioned local secret-provider foundations.

- Parse `Authorization: ApiKey <opaque-key-id>.<secret>`; reject missing/malformed forms.
- Look up opaque key ID, enforce client/key active/expiry/revocation state, compute HMAC-SHA-256 using the verifier’s pepper version, and compare in constant time.
- Use versioned verifiers/pepper metadata. Never return secret after issuance; never log credential material; never reuse key IDs.
- Support one current plus one previous key per client for one-hour rotation overlap; support immediate client disable subject to the 30-second policy cache bound.
- Authenticate before revealing route existence. Use one generic `401` for every invalid key condition; then resolve route (`404` unknown), client allowlist (`403` denied), and route kill-switch.
- Require one active route/client grant before enabling a client. No implicit/unlimited client defaults.
- Sanitize incoming identity/forwarding headers and set trusted `X-Authenticated-Client-Id` and `X-Gateway-Key`.

**Tests:** supported credential parsing, malformed and unknown IDs, wrong secret, key status/expiry, overlap expiry, pepper version, constant-time helper usage review, generic 401 equivalence, authorization outcomes, disable, route kill switch, and policy cache refresh/expiry.

### Slice 4A — Credential persistence and local secret-provider foundations

**Approved scope:** add only the primitives Slice 4 requires: shared credential/grant/store/secret-provider contracts, PostgreSQL EF Core schema and migration for clients, key verifier metadata, and API/version grants, a read-only credential lookup repository, and a local versioned-pepper file adapter. Persist verifier bytes and pepper version only, never raw client secrets. Do not add client-auth middleware, management writes/endpoints, OIDC, auditing, Compose services, policy cache, or key issuance/rotation behavior.

**Status:** implementation and technical checks complete; governance completion is pending human review. PostgreSQL migration/repository lookup was run in an isolated temporary schema, covering missing keys, disabled clients, invalid revocation state, prohibited key deletion, and prohibited key-ID changes; schema tests verify verifier constraints and no raw-secret columns. Local file-provider tests cover safe names/path traversal, symbolic links, missing files, Unix file/directory permissions, and fail-closed Windows behavior. This is not a governance certification. Explicit bounded PostgreSQL connection/command timeouts remain a required host-composition gate before runtime registration. Docker image builds and CI remain deferred to final Phase 1 verification.

### Slice 5 — PostgreSQL policy model and private management API

- Extend the Slice 4A PostgreSQL schema with explicit limits, backend credential verifiers/rotation, route kill switch, optimistic concurrency, and append-only audit events.
- Add private management API and OIDC bearer validation: issuer, audience, signature, expiry, and required `gateway.policy.read` / `gateway.policy.write` scopes.
- Add Keycloak local realm import with non-secret config and `.secrets/`-injected development credentials.
- Implement operations for client create/disable, key issue/rotate/revoke, API/version grants, quota/rate policy, backend credential metadata/rotation, route kill switch, and audit reads within scope.
- Enforce one-time secret response: display generated secret only on create/rotate success; DB and logs contain verifier only. Admin audit stores issuer/subject/client ID, action, target, time, and outcome, never token/key values.
- Apply optimistic concurrency to mutable policy. Management API fails closed with `503` when PostgreSQL is unavailable.
- Implement safe local bootstrap for initial admin, client and route policy; no default production admin or committed credentials.
- Keep management rate limits separate from partner quotas; use conservative configured admin limits.

**Tests:** issuer/audience/signature/expiry/scope; unauthorized vs forbidden; CRUD/state transitions; one-time secret; secret redaction; audit append-only; unique key ID; concurrency stale update; client disable; route kill switch; DB outage; management rate limiting.

### Slice 6 — Local secret handling, TLS, and Compose

- Reuse the provider-neutral secret-provider contract and local ignored-file adapter completed in Slice 4A; cloud adapters are Phase 3/deferred work.
- Fail startup when required secrets/pepper/TLS assets are absent or invalid.
- Generate local development CA/certs into `.secrets/` with a script; document explicit trust installation. Do not commit CA private key, leaf keys, dev passwords, generated API keys, or gateway credentials.
- Use HTTPS client→gateway listener/ingress, gateway→API, gateway→PostgreSQL, and gateway→Redis. Database links use TLS-enabled PostgreSQL/Redis protocols (not HTTPS). Clarify local ingress/TLS ownership in Compose; do not imply an ingress container exists unless added.
- Full Compose default: Gateway, Management, API, Mock, RabbitMQ, PostgreSQL, Redis, Keycloak, and local certificate setup. API, Mock, and data stores are internal; expose the gateway as client entry.
- Infrastructure-only Compose profile: include the dependencies needed by IDE-hosted apps (including current Mock/RabbitMQ paths), with documented ports and TLS behavior.
- Use healthchecks/readiness and dependency retries at orchestration/startup only where safe; no backend request retries.
- Local seed job creates one dev client for TransactionValidation v1 and finite high local limits. Generate key, display once, persist it only in ignored `.secrets/`, and store only verifier in PostgreSQL.

**Tests/checks:** Compose config validation; clean volume initialization; start/stop; TLS trust and hostname checks; service reachability; readiness when dependencies are down; local seed idempotency; `.gitignore`/secret scanning for `.secrets`; no committed credential artifacts.

### Slice 7 — Minimum Phase 1 rate/quota enforcement

Implement the minimum Redis-backed atomic token bucket and daily/monthly UTC quota in Phase 1. This makes the local Compose client usable without bypassing the explicit finite-limits requirement. Phase 2 generalizes and hardens this implementation for additional APIs and operational scenarios; it does not defer first enforcement.

Require explicit finite settings per client/API, including the local seed client. Counters are per client+API shared across versions; charge after authentication/authorization and before rate evaluation/forwarding; return `429` with the appropriate `Retry-After`; fail closed with `503` on Redis failure. Do not seed an unlimited client or bypass checks in tests.

### Slice 8 — Logs, errors, health/readiness, and metrics

- Validate/generate the canonical correlation ID, overwrite client-supplied correlation headers, and return `Correlation-Id` on gateway responses.
- Structured access/security logs contain stable route ID, API/version, client ID/key ID only when safe, source IP, status, latency, decision, and correlation ID. Never log auth headers, secrets, tokens, bodies, or query values.
- Malformed credentials with no parseable ID log only outcome, IP, correlation ID, and route ID.
- Add consistent JSON for gateway-generated errors while passing backend responses unchanged.
- Implement specified statuses: generic `401`, authorized-but-denied `403`, unknown route `404`, invalid method `405`, retired version `410`, oversized request `413`, rate/quota `429`, sanitized proxy failure `502/504`, policy/store `503`.
- Separate liveness and readiness, exempt them from client API-key auth. Readiness is true when the instance can safely process requests: fresh policy cache and Redis available; fresh cached policy may bridge PostgreSQL outage up to 30 seconds.
- Expose network-restricted Prometheus endpoint with low-cardinality counters/histograms for auth failures, denials, limits, backend failures, latency. Never label by client ID or key ID.
- OIDC metadata/JWKS failure is fail closed for management API.

**Tests:** error schema/status; correlation input validation/generation/response; header spoofing; log/audit redaction; metric label inspection; liveness/readiness per dependency state; Prometheus endpoint network exposure review; no backend forwarding on gateway policy errors.

## Phase 1 governance rule matrix

| Authority/rule | Applicability | Planned evidence |
|---|---|---|
| Repository architecture rules, especially Core/project boundaries and no direct public backend | Applies | Project-reference review; Compose/network tests; deployment model review |
| `CS-HOST-01` | Applies to Gateway and Management hosts | Host startup/registration tests |
| `CS-EXT-01` | Applies to YARP/backend, secret, PostgreSQL, Redis, and OIDC boundaries | Adapter/contract tests; no cloud SDK leakage into policy domain |
| `CS-CFG-01` | Applies to route, limits, timeouts, auth, and secret configuration | Valid/invalid/boundary configuration tests; fail-fast behavior |
| `CS-CFG-02` | Applies to client keys, backend credentials, peppers, OIDC secret, certs | Secret scanning, file ignore checks, redaction tests |
| `CS-RES-01` | Applies to bounded calls/cancellation; retries explicitly N/A | Timeout/cancellation tests; verify no proxy retry policy |
| `CS-FLOW-01` | Applies to client/key issuance, rotation/revocation and gateway state transitions | State-transition/audit tests; approval record for exception if needed |
| `CS-HTTP-01` | Applies to Management/Gateway-owned HTTP errors; backend pass-through is separate | Status/content-type/safe-detail tests |
| `CS-OBS-01` | Applies to access/security logs, correlation, metrics | Redaction, context, and cardinality tests |
| `CS-TEST-01` through `CS-TEST-04` | Applicable by unit/host/runtime boundary; `CS-TEST-04` for full Compose e2e | Focused tests, real host, full Compose gateway-backend scenario |
| `CS-MSG-01` | N/A for new Gateway/Management services; backend messaging remains unchanged | Record N/A for new services; retain existing backend messaging tests |
| `CS-VAL-01` | Applies only to finite parsed values; opaque IDs are not enums | Parser tests for key format/policy enums; N/A for opaque identifiers |
| `CS-DEBUG-01` | Conditional on committed shared launch profiles | N/A if none are added; review secrets if added |
| RCFG baseline/dependency/style/workflow/VS Code | Applies to SDK, package, build, formatting and tasks changes | Restore/build/format/CI task review |
| RCFG Compose | Applies to Compose additions | Compose config validation and full-stack test |
| Coverage RCFG rules | Conditional if test coverage artifacts/filters are changed | Confirm new test projects enter intended coverage reports |
| TXV delivery rules | N/A to local Phase 1 | Reassess Phase 3 AWS workflow and federation |

## Required approval decisions and current status

1. **Compatibility validation:** local SDK 10 build, format, unit/integration tests, and existing Docker E2E evidence pass. Docker/.NET 10 compatibility was confirmed by the owner. GitHub Actions and Gateway/Management image builds remain unverified; complete these before claiming full Docker/CI validation.
2. **Local TLS boundary:** approved — Gateway serves local HTTPS directly; a separate local ingress is not part of the current Slice 1 scope.
3. **Seed/bootstrap gate:** approved — use versioned Keycloak realm configuration and local-only bootstrap/secrets. Implementation remains in its later slice.
4. **Worktree scope gate:** approved for Slice 1 project/build setup and the narrow Slice 2 scope recorded above. Preserve pre-existing edits and do not include unrelated untracked/generated artifacts. Later slices require their own scope/design review.

The owner approved Slice 1 on 2026-11-09. Minimum Phase 1 Redis rate/quota enforcement remains a confirmed design decision, but its implementation and remaining validation are not authorized by the Slice 1 approval.

## Related documents

- [YARP gateway implementation phases](yarp-gateway-implementation-plan.md)
- [YARP gateway clarification decisions](yarp-gateway-clarification-decisions.md)
- [Project governance bootstrap](../governance/project_governance_bootstrap.md)
- [AI-assisted development standard](../governance/ai-assisted-development.md)
- [Feature delivery workflow](../governance/feature_delivery_workflow.md)
