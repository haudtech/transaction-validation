# Gateway

## Purpose and scope

This document describes the implemented YARP gateway credential foundation (Phase 1 Slice 4A), Slice 4 request authentication/authorization, and the initial Slice 5 backend-credential PoC. It covers client and backend verifier persistence, local versioned-secret files, OIDC-protected backend-credential management, API-side backend HMAC verification, client-key verification, route authorization, and trusted backend-header preparation.

Client credential management, route kill switches, quotas, policy caching, full Compose deployment, and Docker-backed Gateway/API E2E remain pending.

## Behavior inventory

- Persist client enabled state, client-key verifier metadata, and API/version grants in PostgreSQL.
- Retrieve a credential by opaque key ID, including client state and ordered grants.
- Read a named local secret from a configured directory on Unix-like systems.
- Write generated backend secrets atomically to private local files; return the credential only in a successful issue response.
- Reject unsafe names, symbolic links, empty files, and insecure Unix permissions.
- Fail closed on Windows because this adapter does not validate Windows ACLs.
- Prevent deletion or renaming of persisted key rows, preserving key-ID non-reuse.
- Authenticate partner requests with a key ID and 32-byte Base64URL secret, validate an HMAC-SHA-256 verifier using a versioned pepper, and enforce key/client lifecycle state.
- Return indistinguishable generic `401` responses for invalid credentials before route discovery; apply `404` for unknown routes and `403` for missing API/version grants.
- Apply the explicit POST route/method policy, preserve a canonical correlation ID, remove untrusted auth/identity/forwarding headers, and attach trusted backend identity and credential headers.
- Resolve the current backend key ID and secret reference from PostgreSQL; validate `<key-id>.<secret>` in the API against a stored HMAC-SHA-256 verifier and a dedicated backend pepper.
- Protect management list/issue/revoke endpoints with OIDC bearer validation and `gateway.read` / `gateway.write` scopes; record backend issue/revoke actions in append-only audit storage.
- Configure bounded PostgreSQL connect/command timeouts at Gateway startup and fail closed when credential or backend-secret dependencies are unavailable.

## Technologies and techniques

| Functionality | Technology | Technique | Owner | Verification |
|---|---|---|---|---|
| Credential persistence | EF Core and PostgreSQL | Verifier-only lookup; relational checks, rotation indexes, and append-only audit trigger | `Gateway.PolicyStore` | PostgreSQL migration/repository test |
| Backend credential management | ASP.NET Core minimal APIs and OIDC | Scoped issue/revoke/list endpoints; one-hour previous-key overlap; secret-free audit | `Gateway.Management` | Management host and PostgreSQL integration tests |
| Secret storage | .NET file APIs | Safe filename validation, atomic private writes, symbolic-link rejection, Unix permission checks, no secret logging | `Gateway.Secrets` | Local provider unit tests |
| Cross-project boundaries | Gateway contracts | Store and secret-provider interfaces isolate persistence and file details | `Gateway.Contracts` | Store/provider contract tests |
| Client authentication | Gateway middleware and HMAC-SHA-256 | Parse bounded API-key syntax; validate lifecycle state and compare verifier in constant time | `Gateway` | Gateway authentication integration tests |
| Route authorization and proxy boundary | Gateway middleware and YARP | Check API/version grant and POST method; allowlist forwarded headers and set trusted identity/backend credential | `Gateway` | Gateway proxy/authentication integration tests |

## Definitions and semantics

- **Credential key** - an opaque key ID, a 32-byte HMAC-SHA-256 verifier, a pepper version, status, validity timestamps, and a client ID. Raw client secrets and pepper bytes are not persisted.
- **Client state** - a persisted enabled flag returned with a credential lookup.
- **API/version grant** - an explicit pair of API ID and version associated with a client; Gateway checks it before forwarding.
- **Client API key** - `Authorization: ApiKey <opaque-key-id>.<secret>`, where the secret is 32 cryptographically random bytes encoded as unpadded Base64URL (43 characters). The raw secret is never persisted or forwarded.
- **Previous-key overlap** - one previous key remains valid only until the stored expiry, which must be within one hour of `PreviousSince`; the database permits at most one current and one previous key per client.
- **Backend credential** - `X-Gateway-Key` carries `<opaque-key-id>.<secret>`. Gateway reads only the current key’s secret from the secret provider; API verifies the secret against the persisted HMAC verifier and dedicated pepper version. Previous backend credentials are accepted only within the stored one-hour overlap.
- **Trusted backend headers** - after client authentication, Gateway replaces client-supplied values with `X-Authenticated-Client-Id` and the database-selected backend credential in `X-Gateway-Key`.
- **Local secret** - a non-empty file selected by a safe name beneath the configured root. The root and file must be inaccessible to group and other users on Unix-like systems. Windows use throws `PlatformNotSupportedException` rather than bypassing ACL validation.
- **Key-ID retention** - a database trigger rejects deletion of key rows and changes to their IDs. Revocation is represented by status plus a required timestamp; non-revoked rows must not carry a revocation timestamp.

## Implementation pattern rules

### Governance rule traceability

| Rule | Applicability and implementation | Feature pattern/code evidence | Verification evidence | Exception |
|---|---|---|---|---|
| [CS-EXT-01 — Keep external protocol and SDK details inside adapters](../../governance/coding/coding_standards.md#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters) | PostgreSQL and filesystem details remain behind gateway contracts; Gateway depends on store/provider interfaces. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract); [local secret provider](#pattern-read-local-secrets-with-platform-checks). | Store/provider tests and Gateway host integration tests. | N/A |
| [CS-CFG-02 — Keep secrets out of source-controlled configuration](../../governance/coding/coding_standards.md#cs-cfg-02-keep-secrets-out-of-source-controlled-configuration) | Secret bytes come from the provider; configuration contains a secret name, never client secrets or pepper values. | [Local secret provider](#pattern-read-local-secrets-with-platform-checks); [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Secret-provider and gateway integration tests; `.secrets/` is ignored by `.gitignore`. | N/A |
| [CS-TEST-01 — Test observable behavior at the owning boundary](../../governance/coding/coding_standards.md#cs-test-01-test-observable-behavior-at-the-owning-boundary) | Store invariants use unit/PostgreSQL tests; request auth, management authorization, and proxy behavior use owning host boundaries. | [Credential invariants](#pattern-retain-key-identity-and-revocation-state); [gateway request pipeline](#pattern-authenticate-before-route-discovery); [backend credential management](#pattern-issue-and-rotate-backend-credentials). | Gateway tests; management unauthenticated-host test; opt-in isolated-schema PostgreSQL test. | PostgreSQL and Compose E2E remain pending. |
| [CS-TEST-02 — Make failure scenarios deterministic and safely scoped](../../governance/coding/coding_standards.md#cs-test-02-make-failure-scenarios-deterministic-and-safely-scoped) | Invalid credentials, unavailable dependencies, lifecycle failures, permission denial, and proxy failures use deterministic fakes/local endpoints. | [Failure behavior](#pattern-authenticate-before-route-discovery); [credential invariants](#pattern-retain-key-identity-and-revocation-state). | Gateway tests; PostgreSQL test creates and drops a unique schema. | Signed-token OIDC failures and management outage tests remain pending. |
| [CS-RES-01 — Centralize dependency resilience and preserve cancellation](../../governance/coding/coding_standards.md#cs-res-01-centralize-dependency-resilience-and-preserve-cancellation) | EF lookup propagates cancellation without retries; Gateway configures bounded DB timeouts, fails closed on store/secret outage, and preserves proxy cancellation. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract); [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Cancellation, timeout, dependency-outage, and proxy tests. | N/A |
| [RCFG-BASELINE-01 — Keep SDK, build defaults, and package versions centrally governed](../../governance/configuration/repository_configuration_reference.md#rcfg-baseline-01-keep-sdk-build-defaults-and-package-versions-centrally-governed) | EF and PostgreSQL package versions remain centrally managed. | `Directory.Packages.props` Gateway persistence group. | Release build and central-package review. | N/A |
| [RCFG-DEPENDENCY-01 — Group package and project references by responsibility](../../governance/configuration/repository_configuration_reference.md#rcfg-dependency-01-group-package-and-project-references-by-responsibility) | Package and project references use separate item groups; central packages use labeled responsibility groups. | `Gateway.PolicyStore.csproj`; `Directory.Packages.props`. | Project-file review and Release build. | N/A |
| [RCFG-STYLE-01 — Use `.editorconfig` as the repository style authority](../../governance/configuration/repository_configuration_reference.md#rcfg-style-01-use-editorconfig-as-the-repository-style-authority) | New C# follows repository formatting and analyzer settings. | Changed C# files. | Release build and `dotnet format --verify-no-changes`. | N/A |
| [CS-HOST-01 — Keep executable hosts thin and explicit](../../governance/coding/coding_standards.md#cs-host-01-keep-executable-hosts-thin-and-explicit) | Gateway and Management hosts compose validated services and short middleware/endpoint pipelines; policy logic remains in middleware/services. | [Gateway runtime configuration](#pattern-compose-the-gateway-runtime); [backend credential management](#pattern-issue-and-rotate-backend-credentials). | Gateway and Management host integration tests. | N/A |
| [CS-CFG-01 — Bind and validate configuration at the composition boundary](../../governance/coding/coding_standards.md#cs-cfg-01-bind-and-validate-configuration-at-the-composition-boundary) | Runtime, OIDC, pepper-version, and policy-store settings bind/validate at startup; timeout values and secret names are constrained. | [Gateway runtime configuration](#pattern-compose-the-gateway-runtime); [backend credential management](#pattern-issue-and-rotate-backend-credentials). | Startup validation and host integration tests. | N/A |
| [CS-FLOW-01 — Validate before side effects and make state transitions explicit](../../governance/coding/coding_standards.md#cs-flow-01-validate-before-side-effects-and-make-state-transitions-explicit) | Authentication precedes route disclosure; grant/method checks precede secret retrieval; issuance writes provider state before committing credential metadata and audit together. | [Gateway request pipeline](#pattern-authenticate-before-route-discovery); [backend credential management](#pattern-issue-and-rotate-backend-credentials). | Gateway tests and opt-in PostgreSQL issue/rotation/revoke test. | N/A |
| [CS-OBS-01 — Propagate context while minimizing sensitive telemetry](../../governance/coding/coding_standards.md#cs-obs-01-propagate-context-while-minimizing-sensitive-telemetry) | Cancellation reaches store/secret/proxy calls; credentials are omitted from audit/logging and returned only once at issue. | [Gateway request pipeline](#pattern-authenticate-before-route-discovery); [backend credential management](#pattern-issue-and-rotate-backend-credentials). | Cancellation/correlation tests; append-only audit integration test; source review. | N/A |
| [CS-VAL-01 — Parse finite external values into typed domain values](../../governance/coding/coding_standards.md#cs-val-01-parse-finite-external-values-into-typed-domain-values) | Credential syntax, key-ID characters, secret length/alphabet, client IDs, and route grants are validated before use. | [Gateway request pipeline](#pattern-authenticate-before-route-discovery). | Invalid credential and authorization integration tests. | N/A |
| [CS-TEST-03 — Exercise host integration boundaries without replacing the host](../../governance/coding/coding_standards.md#cs-test-03-exercise-host-integration-boundaries-without-replacing-the-host) | Tests boot the actual Gateway entry point and replace only persistence/secret boundaries. | `GatewayHostFactory`; Gateway authentication/proxy integration tests. | Targeted Gateway test run. | N/A |
| [CS-TEST-04 — Reserve E2E tests for critical runtime contracts](../../governance/coding/coding_standards.md#cs-test-04-reserve-e2e-tests-for-critical-runtime-contracts) | Current tests prove host pipeline and proxy contracts; the full Compose/API backend-authentication path is a required later verification. | Gateway proxy and auth tests. | Existing Docker E2E remains pending for this slice. | Compose E2E not yet wired. |
| [RCFG-COMPOSE-01 — Use Compose as the local multi-service E2E topology](../../governance/configuration/repository_configuration_reference.md#rcfg-compose-01-use-compose-as-the-local-multi-service-e2e-topology); [RCFG-WORKFLOW-01 — Keep GitHub workflow responsibilities explicit and least-privileged](../../governance/configuration/repository_configuration_reference.md#rcfg-workflow-01-keep-github-workflow-responsibilities-explicit-and-least-privileged); [RCFG-VSCODE-01 — Keep workspace launch, settings, and tasks portable](../../governance/configuration/repository_configuration_reference.md#rcfg-vscode-01-keep-workspace-launch-settings-and-tasks-portable) | Not applicable: this slice changes no Compose topology, workflow, VS Code task, or launch profile. | No configuration pattern required. | Scope review. | N/A |
| [RCFG-COVER-UNIT-01](../../governance/configuration/repository_configuration_reference.md#rcfg-cover-unit-01-keep-unit-coverage-collection-isolated); [RCFG-COVER-INT-01](../../governance/configuration/repository_configuration_reference.md#rcfg-cover-int-01-keep-integration-coverage-isolated-from-unit-coverage); [RCFG-CODECOV-01](../../governance/configuration/repository_configuration_reference.md#rcfg-codecov-01-keep-coverage-status-policy-version-controlled) | Not applicable: no test project, coverage collector, report path, or coverage policy changes. | No coverage pattern required. | Project and workflow diff review. | N/A |
| [CS-MSG-01 — Design message consumers for independent at-least-once delivery](../../governance/coding/coding_standards.md#cs-msg-01-design-message-consumers-for-independent-at-least-once-delivery) | Not applicable: Gateway credential persistence and local secret retrieval do not publish or consume broker messages. | No messaging pattern required. | Scope review. | N/A |
| [CS-DEBUG-01 — Keep local debug profiles safe and reproducible](../../governance/coding/coding_standards.md#cs-debug-01-keep-local-debug-profiles-safe-and-reproducible) | Not applicable: no launch or debug profile changes. | No profile pattern required. | Scope review. | N/A |
| [TXV-DEL-01 — Separate infrastructure provisioning from application rollout](../../governance/coding/repository_architecture_rules.md#txv-del-01-separate-infrastructure-provisioning-from-application-rollout); [TXV-DEL-02 — Use federated identity for GitHub-to-Azure workflows](../../governance/coding/repository_architecture_rules.md#txv-del-02-use-federated-identity-for-github-to-azure-workflows); [TXV-DEL-03 — Verify the deployed revision before reporting success](../../governance/coding/repository_architecture_rules.md#txv-del-03-verify-the-deployed-revision-before-reporting-success) | Not applicable: no infrastructure or deployment workflow changes; cloud delivery is deferred from Slice 4. | No delivery pattern required. | Scope review. | N/A |

### Pattern: Read credentials through the store contract

- **Applies to** - credential lookup and PostgreSQL persistence.
- **Intent** - keep database and EF Core details out of gateway-facing consumers.
- **Rule** - callers use `IClientCredentialStore`; the adapter performs one parameterized, no-tracking lookup with cancellation and returns a statement-consistent snapshot of verifier metadata, client state, and API/version grants in deterministic order. It never writes credentials or exposes raw client secrets.
- **Implementation references** - `IClientCredentialStore`; `EfClientCredentialStore`; `GatewayPolicyDbContext`.
- **Verification** - `GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants`, unknown-key and cancellation tests, and the PostgreSQL migration/repository test.
- **Runtime gate** - Gateway composition binds bounded PostgreSQL connection/command timeouts (1–30 seconds) and validates them at startup.

### Pattern: Verify the Gateway credential at the API boundary

- **Applies to** - the trusted `X-Gateway-Key` credential received by the API in Gateway mode.
- **Intent** - make backend authentication depend on persisted verifier metadata rather than a static configured secret.
- **Rule** - parse one bounded `<key-id>.<secret>` value, retrieve the record by API/key ID, enforce status and validity/overlap timestamps, load its separate pepper version, and compare the HMAC-SHA-256 verifier in constant time. Return `401` for invalid credentials and `503` when verifier or pepper dependencies are unavailable; retain the explicit Development-only direct API-key path.
- **Implementation references** - `DatabaseGatewayCredentialVerifier`; `BackendAuthenticationMiddleware`; `IBackendCredentialStore`.
- **Verification** - verifier unit tests cover valid, wrong, expired, and store-outage cases; the opt-in PostgreSQL test exercises management issuance through API verification.

### Pattern: Issue and rotate backend credentials

- **Applies to** - private Management operations for the TransactionValidation backend credential.
- **Intent** - issue a usable credential without storing or re-exposing its raw secret, and permit a bounded rotation overlap.
- **Rule** - require OIDC `gateway.write` scope to issue/revoke and `gateway.read` to list metadata. Store only a peppered HMAC verifier and secret-name reference in PostgreSQL; write the raw secret to the private provider before committing the row, return it only on successful issuance, move the current record to previous for at most one hour, and audit mutations without secret material. Audit rows are append-only.
- **Implementation references** - `GatewayBackendCredentialEndpoints`; `GatewayBackendCredentialManagementService`; `GatewayBackendCredentialsAndAudit`; `LocalFileSecretProvider`.
- **Verification** - management host tests cover missing bearer authentication; the opt-in PostgreSQL integration test covers issuance, API verification, rotation, revocation, and append-only audit.

### Pattern: Retain key identity and revocation state

- **Applies to** - persisted client-key identity and lifecycle metadata.
- **Intent** - prevent accidental key-ID reuse and contradictory revocation state.
- **Rule** - `KeyId` is the primary key; a PostgreSQL trigger rejects row deletion and key-ID updates, preserving identity after revocation. The status constraint requires `RevokedAt` exactly when status is `Revoked`.
- **Implementation references** - `GatewayPolicyDbContext`; `202611090001_InitialCredentialPolicy`.
- **Verification** - schema constraints are asserted, and the PostgreSQL test verifies invalid revocation state, row deletion, and key-ID changes are rejected.

### Pattern: Read and write local secrets with platform checks

- **Applies to** - local versioned secret files.
- **Intent** - provide a local-only secret source without accepting unsafe paths, links, or permissions.
- **Rule** - accept only safe file names, reject reparse points and empty files, verify private Unix modes, create new files atomically without overwriting existing names, and throw on Windows rather than silently skipping ACL validation. Never log secret bytes.
- **Implementation references** - `ISecretProvider`; `LocalFileSecretProvider`.
- **Verification** - `LocalFileSecretProviderTests` cover reads/writes, invalid names, missing files, no-overwrite behavior, symlinks, unsafe permissions, and Windows fail-closed behavior.

### Pattern: Authenticate before route discovery

- **Applies to** - public partner requests to the Gateway host.
- **Intent** - authenticate every non-health request before returning route or permission information.
- **Rule** - parse one `Authorization: ApiKey` value, look up the opaque key ID, validate client/key state and validity times, HMAC the presented secret with the stored pepper version, and compare the verifier in constant time. Invalid credentials share one generic `401`; an authenticated unknown route returns `404`, and a missing API/version grant returns `403`.
- **Implementation references** - `GatewayAuthenticationMiddleware`; `GatewayClientAuthenticator`; `GatewayAuthorizationMiddleware`.
- **Verification** - `GatewayAuthenticationTests` covers equivalent invalid-credential responses, lifecycle cases, overlap, grants, route ordering, dependency outages, health exemption, and canonical correlation; `GatewayProxyTests` verifies trusted-header replacement and client credential removal.

### Pattern: Compose the Gateway runtime

- **Applies to** - executable Gateway startup and dependency registration.
- **Intent** - bind deployment inputs once, validate them before serving requests, and keep persistence and secret details behind contracts.
- **Rule** - register the credential store, local provider, authenticator, and bounded PostgreSQL timeouts through the composition extension. Keep health checks before partner request middleware; fail with `503` when policy or backend-secret dependencies are unavailable.
- **Implementation references** - `Program`; `GatewayServiceCollectionExtensions`; `SecretProviderOptionsValidator`; `GatewayPolicyStore` service registration.
- **Verification** - Gateway startup, unavailable-store/secret, proxy timeout, and cancellation integration tests.

## Local development profile

`docker-compose.gateway.yml` overlays the base Compose file with PostgreSQL, a one-shot `gateway-bootstrap`, the Gateway, and the API in Gateway authentication mode. Set `COMPOSE_FILE=docker-compose.yml:docker-compose.gateway.yml` in `.env` (see `.env.example`) to make a plain `docker compose up` use this profile. Only the Gateway is published (`GATEWAY_HOST_PORT`, default `5100`).

```bash
docker compose -f docker-compose.yml -f docker-compose.gateway.yml up --build
curl -i -X POST "http://localhost:5100/transactions/v1/partner/transactions" \
  -H "Authorization: ApiKey $(cat .secrets/local-client-credential)" ...
```

The bootstrap runs only in `Development`. It applies migrations, creates the peppers, seeds `local-client` with a `transaction-validation`/`v1` grant, writes its credential to `.secrets/local-client-credential`, and issues the first backend credential directly through the Management service (no Management API or Keycloak). It is idempotent and exits non-zero with a message if any step fails. The API and Gateway expose `/healthz` healthchecks, and the Gateway starts only after the API is healthy, so `docker compose up --wait` returns once the profile is ready. After resetting the PostgreSQL volume, delete `.secrets/` as well if the bootstrap reports a missing credential. On Linux hosts, files written by the container may be root-owned. Production secret providers, Keycloak/OIDC, TLS, and rate limits are deferred.

## Configuration contract

| Setting or input | Contract | Purpose |
|---|---|---|
| Local secret root directory | Passed directly to `LocalFileSecretProvider`; reads require a private, non-symlink directory, and the writer creates a missing directory with owner-only permissions. | Locates versioned secret files. |
| Secret name | ASCII letters, digits, hyphens, and underscores; maximum 64 characters; first character must be alphanumeric. | Selects one secret file without permitting path traversal. |
| `Secrets:Provider` | Required. `LocalFile` is the only currently implemented provider and is accepted only in the `Development` environment. | Selects the secret-provider adapter explicitly; unsupported providers fail startup. |
| `Secrets:Directory` | Defaults to `.secrets`; resolved under each host’s content root when `Secrets:Provider` is `LocalFile`. Configure the same shared path for Gateway, API, and Management when running them locally. | Locates local versioned secrets. |
| `Gateway:BackendCredentialManagement:PepperVersion` | Required by the Management host; must be a safe secret filename and refer to a dedicated backend-auth pepper. | Selects the pepper used to create backend credential verifiers. |
| `Management:Oidc:Authority` / `Audience` | Required; authority must use HTTPS. | Configures JWT issuer and audience validation. |
| `Management:Oidc:ReadScope` / `WriteScope` | Defaults to `gateway.read` / `gateway.write`. | Separates metadata reads from credential issue/revoke actions. |
| `Gateway:PolicyStore:ConnectionTimeoutSeconds` / `CommandTimeoutSeconds` | Defaults to 5; each is validated to 1–30 seconds. | Bounds Gateway credential-store database access. |
| `ConnectionStrings:GatewayPolicy` | Required and parsed as an Npgsql connection string at startup. | Configures the Gateway policy-store connection. |
| `GATEWAY_TEST_POSTGRES` | Opt-in test-only connection string. | Runs migration and lookup verification in a uniquely named temporary schema. |

The Gateway, API, and Management hosts use the same `Secrets:Provider` selection and fail startup when it is missing, unsupported, or incompatible with the environment. Management also requires the selected provider to implement `ISecretWriter`. `LocalFile` is currently the only implemented provider and is restricted to Development; production provider adapters and their workload identity configuration remain to be implemented.

## Source ownership

| Project or area | Responsibility |
|---|---|
| `TransactionValidation.Gateway.Contracts` | Credential-store records, grant types, and secret/store interfaces. |
| `TransactionValidation.Gateway.PolicyStore` | PostgreSQL EF model, migrations, client/backend credential lookup, and audit persistence. |
| `TransactionValidation.Gateway.Secrets` | Local file-backed secret reader/writer adapter. |
| `TransactionValidation.Gateway.Management` | OIDC-protected backend credential metadata, issue, rotation, revoke, and audit endpoints. |
| `TransactionValidation.Gateway` | Public YARP host, client authentication, grant/method authorization, correlation, and trusted backend-header preparation. |
| `TransactionValidation.Configuration` | API-side backend verifier and trusted-client-header validation, with explicit development-only direct API-key mode. |
| `TransactionValidation.Tests` | Gateway integration tests, unit tests, and opt-in PostgreSQL verification. |

## Source organization

Gateway source files are grouped by responsibility, and each namespace follows its folder:

| Responsibility | Folder / namespace |
|---|---|
| Host entry points | `Hosting` |
| Request pipeline | `Middleware` |
| Client key authentication | `Authentication` |
| Configuration types | `Options` |
| Configuration validators | `Validators` |
| Dependency registration | `Extensions` |
| API backend authentication | `Configuration/Authentication`, `Configuration/Middleware` |
| Management API | `Management/Endpoints`, `Management/Exceptions`, `Management/Models`, `Management/Options`, `Management/Services` |
| Credential contracts | `Contracts/Interfaces`, `Contracts/Models`, `Contracts/Exceptions` |
| Persistence | `PolicyStore/Context`, `PolicyStore/Entities`, `PolicyStore/Migrations`, `PolicyStore/Options`, `PolicyStore/Stores` |
| Secret adapters | `Secrets/Providers` |

## Verification surface

| Verification | Proves |
|---|---|
| `GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants` | Lookup returns client state, verifier metadata, and grants. |
| `GetByKeyIdAsync_ReturnsNullForUnknownKey` | Missing key IDs do not return a credential. |
| `GetCurrentAsync_ReturnsOnlyTheActiveCurrentCredentialReference` | Gateway resolves only an active current backend credential reference. |
| `VerifyAsync_WhenCredentialMatchesStoredVerifier_ReturnsValid` | API accepts a matching backend secret using its persisted verifier and pepper. |
| `Model_ContainsVerifierConstraintsAndMigrationWithoutRawSecrets` | The EF schema model has verifier/revocation constraints and no raw-secret field. |
| `ManagementIssuance_RotationAndRevocationAreVerifiedByApiAndAudited` | Opt-in PostgreSQL integration covers management issuance, API verification, rotation overlap, revocation, and append-only audit. |
| `LocalFileSecretProviderTests` | Safe reads/writes, non-overwrite behavior, invalid names, missing files, links, permissions, and platform fail-closed behavior. |
| Gateway authentication and proxy integration tests | Generic authentication failures, key lifecycle/overlap, grants, route ordering, backend secret handling, spoofed-header removal, health exemptions, cancellation, and proxy status/error handling. |

## Operational boundaries

- Backend credential issue/rotation/revoke and the API verification path are implemented; client credential management, route kill switches, quotas, policy caching, full Compose wiring, and deployment configuration remain pending.
- The API validates the HMAC-verified backend credential and trusted client-ID header in gateway mode; local direct API-key mode remains development-only.
- Gateway, API gateway-auth mode, and Management require a valid policy-store connection string and deployment-shared versioned secrets.
- Windows local secret-file access is unsupported until an ACL validation implementation and its tests are added.
- PostgreSQL verification is opt-in through `GATEWAY_TEST_POSTGRES`; it creates and drops a uniquely named schema.
- Docker image builds and CI remain deferred to final Phase 1 verification.

## Related supporting documents

- [Gateway Phase 1 implementation proposal](../../plan/yarp-gateway-phase-1-implementation-proposal.md)
- [Gateway Phase 1 code proposal](../../plan/yarp-gateway-phase-1-code-proposal.md)
- [Feature delivery workflow](../../governance/feature_delivery_workflow.md)
- [Validation and Security](../validation-and-security/README.md)
