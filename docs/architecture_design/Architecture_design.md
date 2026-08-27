# Architecture Design Overview

This document provides a high-level architecture view of the TransactionValidation BFF. It focuses on how major components collaborate and how the solution is organized, without implementation-level detail.

This design is aligned to:

- [implementation/implementation_scaffold.md](../implementation/implementation_scaffold.md)
- [implementation/implementation_phases.md](../implementation/implementation_phases.md)
- [implementation/implementation_checklist.md](../implementation/implementation_checklist.md)

## 1. Purpose and Scope

The system accepts partner transaction requests, validates them, applies idempotency checks with cached replay for duplicates, verifies partner identity through an external verification endpoint, and publishes accepted transactions once to a durable topic exchange for independent downstream consumers.

Core capabilities:

- Request intake via BFF API
- API key protection
- Validation and idempotency handling
- Partner verification integration
- Asynchronous message publishing
- Asynchronous message consumption
- Operational observability

## 2. System Context

```mermaid
flowchart LR
    Partner[Partner Client] --> API[TransactionValidation.Api]
    API --> Mock[TransactionValidation.Mock]
    API --> MQ[(RabbitMQ)]
    MQ --> Primary[Primary Consumer Queue]
    MQ --> Audit[Audit Consumer Queue]
    API --> Obs[Telemetry and Logs]
```

## 3. Component Overview

```mermaid
flowchart TB
    subgraph BFF[TransactionValidation BFF]
        Api[TransactionValidation.Api]
        Cfg[TransactionValidation.Configuration]
        Core[TransactionValidation.Core]
        Intg[TransactionValidation.Integration]
        Msg[TransactionValidation.Messaging]
        Primary[Primary Consumer Hosted Service]
        Audit[Audit Consumer Hosted Service]
        Idem[Idempotency Store]
    end

    Partner[Partner Client] --> Api
    Api --> Cfg
    Api --> Core
    Api --> Intg
    Api --> Msg
    Api --> Idem

    Intg --> Mock[TransactionValidation.Mock]
    Msg --> MQ[(RabbitMQ)]
    MQ --> Primary
    MQ --> Audit
```

Responsibilities by project:

- TransactionValidation.Api: API entrypoint, endpoint orchestration, HTTP pipeline.
- TransactionValidation.Configuration: centralized DI, options binding, middleware, exception mapping, telemetry wiring.
- TransactionValidation.Core: domain models, contracts, validation, exceptions.
- TransactionValidation.Integration: partner verification client with resilience policies.
- TransactionValidation.Messaging: exchange publishing abstraction and RabbitMQ implementation.
- TransactionValidation.Mock: local mock provider for partner verification behavior and two hosted RabbitMQ consumers with independent queues.
- TransactionValidation.Tests: unit and integration tests.

## 4. High-Level Runtime Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Client as Partner Client
    participant API as TransactionValidation.Api
    participant Verify as Partner Verification Client
    participant Mock as TransactionValidation.Mock
    participant Publish as Message Publisher
    participant MQ as RabbitMQ
    participant Primary as Primary Consumer
    participant Audit as Audit Consumer

    Client->>API: Submit transaction request
    API->>API: Authenticate, validate, idempotency check

    alt Cached duplicate with same payload
        API-->>Client: Accepted replay response
    else Key reused with different payload
        API-->>Client: ProblemDetails conflict response
    else Fresh request
        API->>Verify: Verify partner
        Verify->>Mock: Verification request
        Mock-->>Verify: Verification response
        Verify-->>API: Verified or failed

        alt Verified and accepted
            API->>Publish: Publish envelope
            Publish->>MQ: Persistent message to partner.transactions with confirm
            MQ-->>Publish: Confirm
            API-->>Client: Accepted
            MQ->>Primary: Deliver copy to partner-transactions
            Primary-->>MQ: Ack after consume
            MQ->>Audit: Deliver copy to partner-transactions.audit
            Audit-->>MQ: Ack after consume
        else Rejected
            API-->>Client: ProblemDetails (404 / 408 / 503)
        end
    end
```

## 5. Configuration and Deployment View

Configuration precedence is intentionally layered for predictable overrides:

1. appsettings.json
2. appsettings.Environment.json
3. environment variables (including values from `.env` when injected by Docker Compose)
4. command-line arguments

Deployment modes:

- Local process mode: API and Mock run from dotnet tooling.
- Container mode: API, Mock, and RabbitMQ run via docker compose.

## 6. Cross-Cutting Concerns

- Security: API key middleware guards external entrypoints.
- Reliability: outbound verification uses resilience policies; queue publishing expects broker confirm semantics.
- Idempotency: duplicate same-payload requests replay the cached accepted response; same-key different-payload requests fail with conflict.
- Error handling: domain exceptions are mapped to RFC 7807 ProblemDetails through centralized exception handling, including upstream `404 Not Found`, `408 Request Timeout`, and `503 Service Unavailable` categories.
- Observability: structured logging and OpenTelemetry pipeline with optional Azure Monitor export.

## 7. Phase Alignment (Overview)

- Phases 1-2: solution structure, core domain contracts, validators.
- Phases 3-4: configuration and middleware foundation, mock verification service.
- Phases 5-6: integration and messaging implementations, endpoint orchestration, idempotency behavior, and publish-to-consume runtime flow.
- Phases 7-8: observability maturity and quality coverage.
- Phases 9-10: containerization, run documentation, and final readiness review.

## 8. Design Boundaries

This architecture overview is intentionally concise and does not define low-level class internals, exact retry values, or exhaustive API contract examples. Those details belong to implementation and operational documents.
