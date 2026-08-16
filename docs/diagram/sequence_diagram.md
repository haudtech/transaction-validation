# API Transaction Sequence Diagram

This diagram shows the end-to-end request flow for `POST /api/v1/partner/transactions` in the current solution.

It focuses on the runtime path through API key authorization, request validation, replay-first idempotency handling, partner verification, message publishing, and error fallback behavior.

Use this diagram to understand the three idempotency outcomes:

- duplicate request with the same payload returns the cached `202 Accepted` response
- duplicate key with a different payload returns `409 Conflict`
- fresh request continues through verification, publish, and cached response storage

It also captures the failure cleanup behavior: if verification or publishing fails after acquisition, the idempotency key is released so a later retry can proceed.

```mermaid
sequenceDiagram
   autonumber
   participant P as Partner/Client
   participant API as TransactionValidation API
   participant Auth as API Key Middleware
   participant Cache as Idempotency Store
   participant Validator as Request Validator
   participant Verifier as PartnerVerifier
   participant Mock as MockPartnerVerification
   participant Publisher as MessagePublisher
   participant MQ as RabbitMQ

   P->>API: POST /api/v1/partner/transactions
   API->>Auth: Validate X-API-Key
   Auth-->>API: Authorized
   API->>Validator: Validate payload
   Validator-->>API: OK / ValidationError
   alt Invalid payload
      API-->>P: 400 Bad Request
   else Valid payload
      API->>API: Build idempotency key
      Note over API: Use Idempotency-Key header when present,\notherwise fallback to partnerId|transactionReference
      API->>API: Build request fingerprint
      API->>Cache: TryAcquire(key, fingerprint)
      alt Duplicate same key and same payload
         Cache-->>API: Duplicate
         API->>Cache: TryGetCachedResponse(key, fingerprint)
         alt Cached accepted response exists
            Cache-->>API: messageId + correlationId + status
            API-->>P: 202 Accepted (replayed cached response)
         else Cache entry missing response
            Cache-->>API: No cached response
            API-->>P: 409 Conflict
         end
      else Same key reused with different payload
         Cache-->>API: KeyReusedWithDifferentPayload
         API-->>P: 409 Conflict
      else Fresh request acquired
         Cache-->>API: Acquired
         API->>Verifier: Verify(partnerId)
         Verifier->>Mock: Call mock verification endpoint
         Mock-->>Verifier: Verified / failure
         alt Verified
            Verifier-->>API: Verified
            API->>Publisher: Publish internal envelope
            Publisher->>MQ: Enqueue persistent message + wait confirms
            MQ-->>Publisher: Ack
            Publisher-->>API: Published
            API->>Cache: StoreCachedResponse(key, fingerprint, accepted response)
            API-->>P: 202 Accepted
         else Verification or publish failed
            Verifier-->>API: Failure
            API->>Cache: Release(key)
            API-->>P: ProblemDetails error response
         end
      end
   end
```
