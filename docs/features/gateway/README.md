# Gateway

## Purpose and scope

This document describes the implemented credential-persistence and local secret-provider foundation for the YARP gateway (Phase 1 Slice 4A). It covers persisted client/key/API-version metadata, the read-only credential lookup contract, and the local versioned-secret file adapter.

This slice does not authenticate partner requests, issue or manage credentials, register the adapters in a runtime host, configure YARP routes, or add management endpoints, policy caching, Compose services, or deployment behavior.

## Behavior inventory

- Persist client enabled state, client-key verifier metadata, and API/version grants in PostgreSQL.
- Retrieve a credential by opaque key ID, including client state and ordered grants.
- Read a named local secret from a configured directory on Unix-like systems.
- Reject unsafe names, symbolic links, empty files, and insecure Unix permissions.
- Fail closed on Windows because this adapter does not validate Windows ACLs.
- Prevent deletion or renaming of persisted key rows, preserving key-ID non-reuse.

## Technologies and techniques

| Functionality | Technology | Technique | Owner | Verification |
|---|---|---|---|---|
| Credential persistence | EF Core and PostgreSQL | Read-only lookup over verifier metadata; relational checks and key-delete trigger | `Gateway.PolicyStore` | PostgreSQL migration/repository test |
| Secret retrieval | .NET file APIs | Safe filename validation, symbolic-link rejection, Unix permission checks, no secret logging | `Gateway.Secrets` | Local provider unit tests |
| Cross-project boundaries | Gateway contracts | Store and secret-provider interfaces isolate persistence and file details | `Gateway.Contracts` | Store/provider contract tests |

## Definitions and semantics

- **Credential key** - an opaque key ID, a 32-byte HMAC-SHA-256 verifier, a pepper version, status, validity timestamps, and a client ID. Raw client secrets and pepper bytes are not persisted.
- **Client state** - a persisted enabled flag returned with a credential lookup.
- **API/version grant** - an explicit pair of API ID and version associated with a client. This slice does not evaluate the grant against a request.
- **Local secret** - a non-empty file selected by a safe name beneath the configured root. The root and file must be inaccessible to group and other users on Unix-like systems. Windows use throws `PlatformNotSupportedException` rather than bypassing ACL validation.
- **Key-ID retention** - a database trigger rejects deletion of key rows and changes to their IDs. Revocation is represented by status plus a required timestamp; non-revoked rows must not carry a revocation timestamp.

## Implementation pattern rules

### Governance rule traceability

| Rule | Applicability and implementation | Feature pattern/code evidence | Verification evidence | Exception |
|---|---|---|---|---|
| [CS-EXT-01 — Keep external protocol and SDK details inside adapters](../../governance/coding/coding_standards.md#cs-ext-01-keep-external-protocol-and-sdk-details-inside-adapters) | PostgreSQL and filesystem details are isolated behind gateway contracts. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract); [local secret provider](#pattern-read-local-secrets-with-platform-checks). | `ClientCredentialStoreTests`; `LocalFileSecretProviderTests`. | N/A |
| [CS-CFG-02 — Keep secrets out of source-controlled configuration](../../governance/coding/coding_standards.md#cs-cfg-02-keep-secrets-out-of-source-controlled-configuration) | The provider reads secret bytes from an untracked local root; it does not log or persist values. | [Local secret provider](#pattern-read-local-secrets-with-platform-checks). | Secret-provider tests; `.secrets/` is ignored by `.gitignore`. | N/A |
| [CS-TEST-01 — Test observable behavior at the owning boundary](../../governance/coding/coding_standards.md#cs-test-01-test-observable-behavior-at-the-owning-boundary) | Store semantics are tested in the store project boundary; real schema invariants use PostgreSQL. | [Credential invariants](#pattern-retain-key-identity-and-revocation-state). | Unit tests and opt-in isolated-schema PostgreSQL test. | N/A |
| [CS-TEST-02 — Make failure scenarios deterministic and safely scoped](../../governance/coding/coding_standards.md#cs-test-02-make-failure-scenarios-deterministic-and-safely-scoped) | Missing files, unsafe permissions, symlinks, invalid revocation state, and attempted key deletion are deterministic cases. | [Failure behavior](#pattern-read-local-secrets-with-platform-checks); [credential invariants](#pattern-retain-key-identity-and-revocation-state). | Unit tests; PostgreSQL test creates and drops a unique schema. | N/A |
| [CS-RES-01 — Centralize dependency resilience and preserve cancellation](../../governance/coding/coding_standards.md#cs-res-01-centralize-dependency-resilience-and-preserve-cancellation) | The EF adapter passes cancellation tokens through database queries and configures no retries. Explicit runtime command/connection timeouts must be owned by `DbContext` composition before a host registers this store. | [PostgreSQL credential lookup](#pattern-read-credentials-through-the-store-contract). | Cancellation test and source review; runtime timeout remains a Slice 5 host-composition gate. | N/A |
| [RCFG-BASELINE-01 — Keep SDK, build defaults, and package versions centrally governed](../../governance/configuration/repository_configuration_reference.md#rcfg-baseline-01-keep-sdk-build-defaults-and-package-versions-centrally-governed) | EF and PostgreSQL package versions remain centrally managed. | `Directory.Packages.props` Gateway persistence group. | Release build and central-package review. | N/A |
| [RCFG-DEPENDENCY-01 — Group package and project references by responsibility](../../governance/configuration/repository_configuration_reference.md#rcfg-dependency-01-group-package-and-project-references-by-responsibility) | Package and project references use separate item groups; central packages use labeled responsibility groups. | `Gateway.PolicyStore.csproj`; `Directory.Packages.props`. | Project-file review and Release build. | N/A |
| [RCFG-STYLE-01 — Use `.editorconfig` as the repository style authority](../../governance/configuration/repository_configuration_reference.md#rcfg-style-01-use-editorconfig-as-the-repository-style-authority) | New C# follows repository formatting and analyzer settings. | Changed C# files. | Release build and `dotnet format --verify-no-changes`. | N/A |
| [CS-HOST-01 — Keep executable hosts thin and explicit](../../governance/coding/coding_standards.md#cs-host-01-keep-executable-hosts-thin-and-explicit) | Not applicable: no executable host or startup composition is added. | No implementation pattern required. | Scope review. | N/A |
| [CS-CFG-01 — Bind and validate configuration at the composition boundary](../../governance/coding/coding_standards.md#cs-cfg-01-bind-and-validate-configuration-at-the-composition-boundary) | Not applicable: no runtime options or host-bound settings are introduced; the local provider receives its root path directly. | No implementation pattern required. | Scope review. | N/A |
| [CS-FLOW-01 — Validate before side effects and make state transitions explicit](../../governance/coding/coding_standards.md#cs-flow-01-validate-before-side-effects-and-make-state-transitions-explicit) | Not applicable: this slice adds no management or mutation workflow. Persisted lifecycle fields are constrained and read only. | No workflow pattern required. | PostgreSQL constraint test verifies revocation state. | N/A |
| [CS-OBS-01 — Propagate context while minimizing sensitive telemetry](../../governance/coding/coding_standards.md#cs-obs-01-propagate-context-while-minimizing-sensitive-telemetry) | Not applicable: this slice adds no logs, traces, metrics, or health signals; secret bytes are not logged. | No observability pattern required. | Source review. | N/A |
| [CS-VAL-01 — Parse finite external values into typed domain values](../../governance/coding/coding_standards.md#cs-val-01-parse-finite-external-values-into-typed-domain-values) | Not applicable: key and API identifiers are opaque/open-ended strings, not members of a finite set. | No parser or enum required. | Contract and schema review. | N/A |
| [CS-TEST-03 — Exercise host integration boundaries without replacing the host](../../governance/coding/coding_standards.md#cs-test-03-exercise-host-integration-boundaries-without-replacing-the-host) | Not applicable: no host, middleware, or service registration changes in this slice. | No host integration pattern required. | Scope review. | N/A |
| [CS-TEST-04 — Reserve E2E tests for critical runtime contracts](../../governance/coding/coding_standards.md#cs-test-04-reserve-e2e-tests-for-critical-runtime-contracts) | Not applicable: no runtime topology or partner request behavior is added; the PostgreSQL test covers the changed persistence boundary. | No E2E topology change required. | PostgreSQL migration/repository test. | N/A |
| [RCFG-COMPOSE-01 — Use Compose as the local multi-service E2E topology](../../governance/configuration/repository_configuration_reference.md#rcfg-compose-01-use-compose-as-the-local-multi-service-e2e-topology); [RCFG-WORKFLOW-01 — Keep GitHub workflow responsibilities explicit and least-privileged](../../governance/configuration/repository_configuration_reference.md#rcfg-workflow-01-keep-github-workflow-responsibilities-explicit-and-least-privileged); [RCFG-VSCODE-01 — Keep workspace launch, settings, and tasks portable](../../governance/configuration/repository_configuration_reference.md#rcfg-vscode-01-keep-workspace-launch-settings-and-tasks-portable) | Not applicable: this slice changes no Compose topology, workflow, VS Code task, or launch profile. | No configuration pattern required. | Scope review. | N/A |
| [RCFG-COVER-UNIT-01](../../governance/configuration/repository_configuration_reference.md#rcfg-cover-unit-01-keep-unit-coverage-collection-isolated); [RCFG-COVER-INT-01](../../governance/configuration/repository_configuration_reference.md#rcfg-cover-int-01-keep-integration-coverage-isolated-from-unit-coverage); [RCFG-CODECOV-01](../../governance/configuration/repository_configuration_reference.md#rcfg-codecov-01-keep-coverage-status-policy-version-controlled) | Not applicable: no test project, coverage collector, report path, or coverage policy changes. | No coverage pattern required. | Project and workflow diff review. | N/A |
| [CS-MSG-01 — Design message consumers for independent at-least-once delivery](../../governance/coding/coding_standards.md#cs-msg-01-design-message-consumers-for-independent-at-least-once-delivery) | Not applicable: Gateway credential persistence and local secret retrieval do not publish or consume broker messages. | No messaging pattern required. | Scope review. | N/A |
| [CS-DEBUG-01 — Keep local debug profiles safe and reproducible](../../governance/coding/coding_standards.md#cs-debug-01-keep-local-debug-profiles-safe-and-reproducible) | Not applicable: no launch or debug profile changes. | No profile pattern required. | Scope review. | N/A |
| [TXV-DEL-01 — Separate infrastructure provisioning from application rollout](../../governance/coding/repository_architecture_rules.md#txv-del-01-separate-infrastructure-provisioning-from-application-rollout); [TXV-DEL-02 — Use federated identity for GitHub-to-Azure workflows](../../governance/coding/repository_architecture_rules.md#txv-del-02-use-federated-identity-for-github-to-azure-workflows); [TXV-DEL-03 — Verify the deployed revision before reporting success](../../governance/coding/repository_architecture_rules.md#txv-del-03-verify-the-deployed-revision-before-reporting-success) | Not applicable: no infrastructure or deployment workflow changes; cloud delivery is deferred from local Slice 4A. | No delivery pattern required. | Scope review. | N/A |

### Pattern: Read credentials through the store contract

- **Applies to** - credential lookup and PostgreSQL persistence.
- **Intent** - keep database and EF Core details out of gateway-facing consumers.
- **Rule** - callers use `IClientCredentialStore`; the adapter performs one parameterized, no-tracking lookup with cancellation and returns a statement-consistent snapshot of verifier metadata, client state, and API/version grants in deterministic order. It never writes credentials or exposes raw client secrets.
- **Implementation references** - `IClientCredentialStore`; `EfClientCredentialStore`; `GatewayPolicyDbContext`.
- **Verification** - `GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants`, unknown-key and cancellation tests, and the PostgreSQL migration/repository test.
- **Runtime gate** - the host that first registers this store must configure explicit bounded PostgreSQL connection/command timeouts; no runtime host is introduced in Slice 4A.

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

## Configuration contract

| Setting or input | Contract | Purpose |
|---|---|---|
| Local secret root directory | Passed directly to `LocalFileSecretProvider`; must exist and be a private, non-symlink directory on Unix-like systems. | Locates versioned secret files. |
| Secret name | ASCII letters, digits, hyphens, and underscores; maximum 64 characters; first character must be alphanumeric. | Selects one secret file without permitting path traversal. |
| `GATEWAY_TEST_POSTGRES` | Opt-in test-only connection string. | Runs migration and lookup verification in a uniquely named temporary schema. |

No runtime host configuration or production secret-delivery path is introduced in this slice.

## Source ownership

| Project or area | Responsibility |
|---|---|
| `TransactionValidation.Gateway.Contracts` | Credential-store records, grant types, and secret/store interfaces. |
| `TransactionValidation.Gateway.PolicyStore` | PostgreSQL EF model, migration, and read-only credential lookup. |
| `TransactionValidation.Gateway.Secrets` | Local file-backed secret-provider adapter. |
| `TransactionValidation.Tests` | Unit and opt-in PostgreSQL verification. |

## Verification surface

| Verification | Proves |
|---|---|
| `GetByKeyIdAsync_ReturnsCredentialClientStateAndGrants` | Lookup returns client state, verifier metadata, and grants. |
| `GetByKeyIdAsync_ReturnsNullForUnknownKey` | Missing key IDs do not return a credential. |
| `Model_ContainsVerifierConstraintsAndMigrationWithoutRawSecrets` | The EF schema model has verifier/revocation constraints and no raw-secret field. |
| `PostgreSqlMigrationAndStore_ReadCredentialFromIsolatedSchema` | PostgreSQL migration-up and rollback work in isolation; invalid revocation state, row deletion, and key-ID changes are rejected. |
| `LocalFileSecretProviderTests` | Safe reads, invalid names, missing files, links, permissions, and platform fail-closed behavior. |

## Operational boundaries

- The contracts and adapters are not registered in Gateway or Management runtime hosts yet.
- Authentication, secret issuance/rotation, management writes, database provisioning, policy caching, and deployment configuration are not implemented.
- Before runtime registration, host composition must define bounded PostgreSQL connection and command timeouts; the adapter propagates caller cancellation and adds no retries.
- Windows local secret-file access is unsupported until an ACL validation implementation and its tests are added.
- PostgreSQL verification is opt-in through `GATEWAY_TEST_POSTGRES`; it creates and drops a uniquely named schema.
- Docker image builds and CI remain deferred to final Phase 1 verification.

## Related supporting documents

- [Gateway Phase 1 implementation proposal](../../plan/yarp-gateway-phase-1-implementation-proposal.md)
- [Gateway Phase 1 code proposal](../../plan/yarp-gateway-phase-1-code-proposal.md)
- [Feature delivery workflow](../../governance/feature_delivery_workflow.md)
- [Validation and Security](../validation-and-security/README.md)
