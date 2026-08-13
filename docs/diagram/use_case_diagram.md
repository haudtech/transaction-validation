# Use Case Diagram — Partner Integration BFF

This diagram captures the main actors and use cases for the interview assignment, based on `docs/reqs/Senior_net_interview_2026.md` and `docs/analysis/solution_analysis.md`.

```mermaid
usecaseDiagram
    actor Partner as P
    actor BFF as B
    actor MessageBroker as MQ
    actor MockPartnerVerification as MVP

    P --> (Submit Partner Transaction)
    B --> (Validate Transaction Payload)
    B --> (Authorize Request with API Key)
    B --> (Verify Partner)
    B --> (Enrich and Transform Message)
    B --> (Publish to Message Queue)
    MQ --> (Acknowledge Message Receipt)
    MVP --> (Respond to Partner Verification Request)

    (Submit Partner Transaction) .> (Authorize Request with API Key) : includes
    (Submit Partner Transaction) .> (Validate Transaction Payload) : includes
    (Submit Partner Transaction) .> (Verify Partner) : includes
    (Verify Partner) .> (Respond to Partner Verification Request) : uses
    (Verify Partner) .> (Enrich and Transform Message) : success
    (Enrich and Transform Message) .> (Publish to Message Queue) : includes
    (Publish to Message Queue) .> (Acknowledge Message Receipt) : uses

    note right of P : Partner or partner-facing client
    note right of B : Backend-for-Frontend microservice
    note right of MQ : Local message broker (RabbitMQ)
    note right of MVP : Mock verification endpoint with 30% timeouts
```
