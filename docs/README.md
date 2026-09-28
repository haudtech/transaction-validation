# Documentation Tree

This is the main navigation entry point for the TransactionValidation documentation set. Start with the [platform overview](../README.md), then use this tree to find the authoritative guide for the relevant concern.

## Documentation authority

- Application code, configuration, workflows, and infrastructure definitions remain the source of truth for runtime behavior.
- The root project README explains the platform and supported capabilities.
- This page is the documentation index and navigation map.
- Each domain area owns the authoritative content for its concern and links to neighboring domains instead of duplicating them.
- Generated reports and historical implementation records are not permanent system documentation.

## 1. Architecture and system semantics

These documents define the stable design boundaries, runtime contracts, and cross-cutting semantics.

- [Architecture design overview](architecture_design/Architecture_design.md): system context, project responsibilities, runtime flow, configuration, and deployment modes. Primary owners: all solution projects.
- [API idempotency flow and semantics](architecture_design/api_idempotency_flow_and_semantics.md): request identity, payload fingerprinting, replay rules, conflict handling, expiry, and accepted-response replacement. Primary owner: `TransactionValidation.Api`.
- [Failure, timeout, and retry flow](architecture_design/failure_timeout_retry_flow.md): recovery behavior, timeout handling, release-after-failure, and safe re-entry after partial outcomes. Primary owner: `TransactionValidation.Api`.
- [Messaging topology and consumer routing](architecture_design/messaging_topology_and_consumer_routing.md): topology, routing, independent consumer paths, lifecycle behavior, and ownership boundaries. Primary owners: `TransactionValidation.Messaging` and `TransactionValidation.Mock`.

## 2. Feature domains

These are the authoritative behavioral and domain guides for the implemented system.

### API and runtime behavior
- [Runtime and API](features/runtime-and-api/README.md): executable hosts, endpoint contract, API discovery, configuration precedence, and runtime composition.
- [Validation and security](features/validation-and-security/README.md): request validation, API-key and correlation middleware, and HTTP error boundaries.
- [Reliability](features/reliability/README.md): partner verification, timeout decisions, circuit breakers, outcome translation, cancellation, and retry-safe behavior.

### Messaging and state
- [Messaging feature](features/messaging/README.md): broker selection, publication contracts, metadata, implementation patterns, config shape, and verification boundaries.
- [Distributed state](features/distributed-state/README.md): in-memory and Redis idempotency storage, TTL, concurrency, replay state, and failure-release semantics.

### Observability and quality
- [Observability feature](features/observability/README.md): logging, correlation, tracing, metrics, timing, health, and export boundaries.
- [Testing and Quality feature](features/testing-and-quality/README.md): unit, integration-host, and E2E responsibilities; coverage collection; quality gates; and evidence policy.

### Platform and delivery
- [Local platform feature](features/local-platform/README.md): local runtime topology, environment composition, and local platform boundaries.
- [Azure platform feature](features/azure-platform/README.md): Azure resource model, identity boundaries, deployment outputs, and platform responsibilities.
- [Infrastructure and delivery feature](features/infrastructure-and-delivery/README.md): CI, Azure OIDC, infrastructure preview/apply flow, and application deployment boundaries.

## 3. Operations and support

These docs explain how to run, validate, diagnose, and maintain the system in local and deployed environments.

- [Operations documentation](operations/README.md): entry point for local startup, deployment, testing, diagnostics, and runtime support.
- [Local development guide](operations/local/local_development.md): SDK setup, Docker Compose startup, environment configuration, health checks, and shutdown flow.
- [Test execution and coverage guide](operations/testing/test/test_execution_and_coverage_guide.md): unit, integration, E2E, coverage, and report generation workflows. Primary owner: `TransactionValidation.Tests`.
- [E2E runtime matrix](operations/testing/test/e2e_runtime_matrix.md): deployed-runtime scenarios for HTTP, idempotency, broker fan-out, routing, and redelivery.
- [GitHub Actions documentation](operations/deployment/workflows/github/README.md): CI, integration, infrastructure, and deployment workflow entry points.
- [Azure deployment documentation](operations/deployment/azure_deployment/README.md): identity setup, Azure CLI commands, environment validation, and operational recovery procedures.
- [Logging standards](operations/diagnostics/logging_standards.md): source-generated logging catalog, event IDs, structured fields, privacy rules, and analyzer enforcement. Primary owner: `TransactionValidation.Core`.

## 4. Governance and standards

These docs define durable repo-wide rules, ownership boundaries, and documentation policy.

- [Repository governance](governance/README.md): durable repository standards, policy boundaries, and governing rules.
- [Documentation standards](governance/documentation_standards.md): authoring rules, ownership boundaries, and evidence-first policies.
- [Repository architecture rules](governance/repository_architecture_rules.md): project boundaries, dependency direction, and repository-specific engineering constraints.

## Recommended reading flow

1. Read the project overview and architecture overview.
2. Review the API idempotency semantics and messaging topology.
3. Choose the relevant domain feature guide for runtime behavior.
4. Use the operations docs for execution, diagnosis, and deployment support.
5. Consult governance only for repo-wide rules and documentation authority.
