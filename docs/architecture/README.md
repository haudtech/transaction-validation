# Architecture

This folder contains the canonical architecture and runtime semantics for the solution. It is the authoritative layer for cross-cutting design concerns and system composition.

## Scope

Architecture docs describe:

- system boundaries and project responsibilities
- runtime composition and request flow
- cross-cutting semantics such as idempotency and failure handling
- messaging topology and routing ownership
- operational and deployment constraints at the architectural level

## Documents

- [Architecture design overview](Architecture_design.md): system context, project responsibilities, runtime flow, configuration model, and deployment modes.
- [API idempotency flow and semantics](api_idempotency_flow_and_semantics.md): request identity, payload fingerprinting, replay rules, conflict handling, expiry, and accepted-response replacement.
- [Failure, timeout, and retry flow](failure_timeout_retry_flow.md): recovery behavior, timeout handling, release-after-failure, and safe re-entry semantics.
- [Messaging topology and consumer routing](messaging_topology_and_consumer_routing.md): broker topology, routing, independent consumer paths, and lifecycle behavior.

## Ownership boundary

Architecture docs are not runbooks. They are not feature specs, and they do not duplicate operational procedures. For execution guidance, use the [operations docs](../operations/README.md). For repository policy, use the [governance docs](../governance/README.md).
