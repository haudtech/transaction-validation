# Gateway

## Purpose and scope

This document describes the implemented YARP gateway credential foundation (Phase 1 Slice 4A) and Slice 4 request authentication/authorization. It covers persisted client/key/API-version metadata, the read-only credential lookup contract, local versioned-secret files, gateway host composition, client-key verification, route authorization, and trusted backend-header preparation.

Credential issuance/rotation management, route kill switches, policy caching, Compose-backed gateway/API E2E, and deployment behavior remain outside these slices.

## Behavior inventory

- Persist client enabled state, client-key verifier metadata, and API/version grants in PostgreSQL.
- Retrieve a credential by opaque key ID, including client state and ordered grants.
- Read a named local secret from a configured directory on Unix-like systems.
- Reject unsafe names, symbolic links, empty files, and insecure Unix permissions.
- Fail closed on Windows because this adapter does not validate Windows ACLs.
- Prevent deletion or renaming of persisted key rows, preserving key-ID non-reuse.
- Authenticate partner requests with a key ID and 32-byte Base64URL secret, validate an HMAC-SHA-256 verifier using a versioned pepper, and enforce key/client lifecycle state.
- Return indistinguishable generic `401` responses for invalid credentials before route discovery; apply `404` for unknown routes and `403` for missing API/version grants.
- Apply the explicit POST route/method policy, preserve a canonical correlation ID, remove untrusted auth/identity/forwarding headers, and attach trusted backend identity and credential headers.
- Configure bounded PostgreSQL connect/command timeouts at Gateway startup and fail closed when credential or backend-secret dependencies are unavailable.

## Technologies and techniques

| Functionality | Technology | Technique | Owner | Verification |
|---|---|---|---|---|
| Credential persistence | EF Core and PostgreSQL | Read-only lookup over verifier metadata; relational checks and key-delete trigger | `Gateway.PolicyStore` | PostgreSQL migration/repository test |
| Secret retrieval | .NET file APIs | Safe filename validation, symbolic-link rejection, Unix permission checks, no secret logging | `Gateway.Secrets` | Local provider unit tests |
| Cross-project boundaries | Gateway contracts | Store and secret-provider interfaces isolate persistence and file details | `Gateway.Contracts` | Store/provider contract tests |
| Client authentication | Gateway middleware and HMAC-SHA-256 | Parse bounded API-key syntax; validate lifecycle state and compare verifier in constant time | `Gateway` | Gateway authentication integration tests |
| Route authorization and proxy boundary | Gateway middleware and YARP | Check API/version grant and POST method; allowlist forwarded headers and set trusted identity/backend credential | `Gateway` | Gateway proxy/authentication integration tests |

## Definitions and semantics

- **Credential key** - an opaque key ID, a 32-byte HMAC-SHA-256 verifier, a pepper version, status, validity timestamps, and a client ID. Raw client secrets and pepper bytes are not persisted.
- **Client state** - a persisted enabled flag returned with a credential lookup.
- **API/version grant** - an explicit pair of API ID and version associated with a client; Gateway checks it before forwarding.
- **Client API key** - `Authorization: ApiKey <opaque-key-id>.<secret>`, where the secret is 32 cryptographically random bytes encoded as unpadded Base64URL (43 characters). The raw secret is never persisted or forwarded.
- **Previous-key overlap** - one previous key remains valid only until the stored expiry, which must be within one hour of `PreviousSince`; the database permits at most one current and one previous key per client.
- **Trusted backend headers** - after client authentication, the gateway replaces any client-supplied copies with `X-Authenticated-Client-Id` and the configured backend credential in `X-Gateway-Key`.
- **Local secret** - a non-empty file selected by a safe name beneath the configured root. The root and file must be inaccessible to group and other users on Unix-like systems. Windows use throws `PlatformNotSupportedException` rather than bypassing ACL validation.
- **Key-ID retention** - a database trigger rejects deletion of key rows and changes to their IDs. Revocation is represented by status plus a required timestamp; non-revoked rows must not carry a revocation timestamp.

## Implementation pattern rules

### Governance rule traceability

| Rule | Applicability and implementation | Feature pattern/code evidence | Verification evidence | Exception |
|---|---|---|---|---|
| [CS-EXT-01 — Keep external protocol and SDK details inside adapters](../../governance/coding/coding_standards.md#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters) | PostgreSQL and filesystem details remain behind gateway contracts; Gateway depends on store/provider interfaces. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract); [local secret provider](#pattern-read-local-secrets-with-platform-checks). | Store/provider tests and Gateway host integration tests. | N/A |
| [CS-CFG-02 — Keep secrets out of source-controlled configuration](../../governance/coding/coding_standards.md#cs-cfg-02-keep-secrets-out-of-source-controlled-configuration) | Secret bytes come from the provider; configuration contains a secret name, never client secrets or pepper values. | [Local secret provider](#pattern-read-local-secrets-with-platform-checks); [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Secret-provider and gateway integration tests; `.secrets/` is ignored by `.gitignore`. | N/A |
| [CS-TEST-01 — Test observable behavior at the owning boundary](../../governance/coding/coding_standards.md#cs-test-01-test-observable-behavior-at-the-owning-boundary) | Store invariants use unit/PostgreSQL tests; request auth, authorization, and proxy behavior use the real Gateway host. | [Credential invariants](#pattern-retain-key-identity-and-revocation-state); [gateway request pipeline](#pattern-authenticate-before-route-discovery). | Gateway tests and opt-in isolated-schema PostgreSQL test. | Compose-backed E2E remains pending. |
| [CS-TEST-02 — Make failure scenarios deterministic and safely scoped](../../governance/coding/coding_standards.md#cs-test-02-make-failure-scenarios-deterministic-and-safely-scoped) | Invalid credentials, unavailable dependencies, lifecycle failures, permission denial, and proxy failures use deterministic fakes/local endpoints. | [Failure behavior](#pattern-authenticate-before-route-discovery); [credential invariants](#pattern-retain-key-identity-and-revocation-state). | Gateway tests; PostgreSQL test creates and drops a unique schema. | N/A |
| [CS-RES-01 — Centralize dependency resilience and preserve cancellation](../../governance/coding/coding_standards.md#cs-res-01-centralize-dependency-resilience-and-preserve-cancellation) | EF lookup propagates cancellation without retries; Gateway configures bounded DB timeouts, fails closed on store/secret outage, and preserves proxy cancellation. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract); [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Cancellation, timeout, dependency-outage, and proxy tests. | N/A |
| [RCFG-BASELINE-01 — Keep SDK, build defaults, and package versions centrally governed](../../governance/configuration/repository_configuration_reference.md#rcfg-baseline-01-keep-sdk-build-defaults-and-package-versions-centrally-governed) | EF and PostgreSQL package versions remain centrally managed. | `Directory.Packages.props` Gateway persistence group. | Release build and central-package review. | N/A |
| [RCFG-DEPENDENCY-01 — Group package and project references by responsibility](../../governance/configuration/repository_configuration_reference.md#rcfg-dependency-01-group-package-and-project-references-by-responsibility) | Package and project references use separate item groups; central packages use labeled responsibility groups. | `Gateway.PolicyStore.csproj`; `Directory.Packages.props`. | Project-file review and Release build. | N/A |
| [RCFG-STYLE-01 — Use `.editorconfig` as the repository style authority](../../governance/configuration/repository_configuration_reference.md#rcfg-style-01-use-editorconfig-as-the-repository-style-authority) | New C# follows repository formatting and analyzer settings. | Changed C# files. | Release build and `dotnet format --verify-no-changes`. | N/A |
| [CS-HOST-01 — Keep executable hosts thin and explicit](../../governance/coding/coding_standards.md#cs-host-01-keep-executable-hosts-thin-and-explicit) | The host composes validated services and a short middleware pipeline; policy logic remains in middleware/services. | [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Gateway startup and host integration tests. | N/A |
| [CS-CFG-01 — Bind and validate configuration at the composition boundary](../../governance/coding/coding_standards.md#cs-cfg-01-bind-and-validate-configuration-at-the-composition-boundary) | Runtime and policy-store settings bind/validate on startup; timeout values are bounded and secret names are constrained. | [Gateway runtime configuration](#pattern-compose-the-gateway-runtime). | Startup validation and Gateway host integration tests. | N/A |
| [CS-FLOW-01 — Validate before side effects and make state transitions explicit](../../governance/coding/coding_standards.md#cs-flow-01-validate-before-side-effects-and-make-state-transitions-explicit) | Authentication precedes route disclosure; grant/method checks precede backend-secret retrieval and proxying. | [Gateway request pipeline](#pattern-authenticate-before-route-discovery). | Integration tests prove invalid requests do not reach backend. | N/A |
| [CS-OBS-01 — Propagate context while minimizing sensitive telemetry](../../governance/coding/coding_standards.md#cs-obs-01-propagate-context-while-minimizing-sensitive-telemetry) | Request cancellation reaches store/secret/proxy calls; canonical correlation IDs are propagated; credentials are not logged or returned. | [Gateway request pipeline](#pattern-authenticate-before-route-discovery). | Cancellation and correlation integration tests; source review. | N/A |
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

### Pattern: Retain key identity and revocation state

- **Applies to** - persisted client-key identity and lifecycle metadata.
- **Intent** - prevent accidental key-ID reuse and contradictory revocation state.
- **Rule** - `KeyId` is the primary key; a PostgreSQL trigger rejects row deletion and key-ID updates, preserving identity after revocation. The status constraint requires `RevokedAt` exactly when status is `Revoked`.
- **Implementation references** - `GatewayPolicyDbContext`; `202611090001_InitialCredentialPolicy`.
- **Verification** - schema constraints are asserted, and the PostgreSQL test verifies invalid revocation state, row deletion, and key-ID changes are rejected.

### Pattern: Read local secrets with platform checks

- **Applies to** - local versioned secret files.
- **Intent** - provide a local-only secret source without accepting unsafe paths, links, or permissions.
- **Rule** - accept only safe file names, reject reparse points and empty files, verify private Unix modes, and throw on Windows rather than silently skipping ACL validation. Never log secret bytes.
- **Implementation references** - `ISecretProvider`; `LocalFileSecretProvider`.
- **Verification** - `LocalFileSecretProviderTests` cover success, invalid names, missing files, symlinks, unsafe permissions, and the Windows fail-closed behavior.

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
- **Implementation references** - `Program`; `GatewayServiceCollectionExtensions`; `GatewayRuntimeOptionsValidator`; `GatewayPolicyStore` service registration.
- **Verification** - Gateway startup, unavailable-store/secret, proxy timeout, and cancellation integration tests.

## Configuration contract

| Setting or input | Contract | Purpose |
|---|---|---|
| Local secret root directory | Passed directly to `LocalFileSecretProvider`; must exist and be a private, non-symlink directory on Unix-like systems. | Locates versioned secret files. |
| Secret name | ASCII letters, digits, hyphens, and underscores; maximum 64 characters; first character must be alphanumeric. | Selects one secret file without permitting path traversal. |
| `Gateway:SecretsDirectory` | Defaults to `.secrets`; validated at startup and resolved under the Gateway content root. | Locates local peppers and the backend credential secret. |
| `Gateway:BackendCredentialSecretName` | Defaults to `transaction-validation-backend`; must be a safe secret filename. | Selects the separate backend credential sent in `X-Gateway-Key`. |
| `Gateway:PolicyStore:ConnectionTimeoutSeconds` / `CommandTimeoutSeconds` | Defaults to 5; each is validated to 1–30 seconds. | Bounds Gateway credential-store database access. |
| `ConnectionStrings:GatewayPolicy` | Required and parsed as an Npgsql connection string at startup. | Configures the Gateway policy-store connection. |
| `GATEWAY_TEST_POSTGRES` | Opt-in test-only connection string. | Runs migration and lookup verification in a uniquely named temporary schema. |

Production secrets must be supplied through the configured `ISecretProvider`; the local file provider is only the local adapter.

## Source ownership

| Project or area | Responsibility |
|---|---|
| `TransactionValidation.Gateway.Contracts` | Credential-store records, grant types, and secret/store interfaces. |
| `TransactionValidation.Gateway.PolicyStore` | PostgreSQL EF model, migration, and read-only credential lookup. |
| `TransactionValidation.Gateway.Secrets` | Local file-backed secret-provider adapter. |
| `TransactionValidation.Gateway` | Public YARP host, credential authentication, grant/method authorization, correlation, and trusted backend headers. |
| `TransactionValidation.Configuration` | Backend gateway-credential and trusted-client-header validation, with explicit development-only direct API-key mode. |
| `TransactionValidation.Tests` | Gateway integration tests, unit tests, and opt-in PostgreSQL verification. |

## Verification surface

| Verification | Proves |
|---|---|
| `GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants` | Lookup returns client state, verifier metadata, and grants. |
| `GetByKeyIdAsync_ReturnsNullForUnknownKey` | Missing key IDs do not return a credential. |
| `Model_ContainsVerifierConstraintsAndMigrationWithoutRawSecrets` | The EF schema model has verifier/revocation constraints and no raw-secret field. |
| `PostgreSqlMigrationAndStore_ReadCredentialFromIsolatedSchema` | PostgreSQL migration-up and rollback work in isolation; invalid revocation state, row deletion, and key-ID changes are rejected. |
| `LocalFileSecretProviderTests` | Safe reads, invalid names, missing files, links, permissions, and platform fail-closed behavior. |
| Gateway authentication and proxy integration tests | Generic authentication failures, key lifecycle/overlap, grants, route ordering, backend secret handling, spoofed-header removal, health exemptions, cancellation, and proxy status/error handling. |

## Operational boundaries

- Credential authentication and route authorization are implemented in the Gateway host; secret issuance/rotation, management writes, route kill switches, policy caching, Compose wiring, and deployment configuration are not implemented here.
- The API validates the trusted backend credential and client-ID headers in gateway mode; local direct API-key mode remains development-only.
- Gateway startup requires a valid policy-store connection string and versioned secrets; local secret-file access is unsupported on Windows until ACL validation is implemented.
- Windows local secret-file access is unsupported until an ACL validation implementation and its tests are added.
- PostgreSQL verification is opt-in through `GATEWAY_TEST_POSTGRES`; it creates and drops a uniquely named schema.
- Docker image builds and CI remain deferred to final Phase 1 verification.

## Related supporting documents

- [Gateway Phase 1 implementation proposal](../../plan/yarp-gateway-phase-1-implementation-proposal.md)
- [Gateway Phase 1 code proposal](../../plan/yarp-gateway-phase-1-code-proposal.md)
- [Feature delivery workflow](../../governance/feature_delivery_workflow.md)
- [Validation and Security](../validation-and-security/README.md)
