# Adjustment Implementation Plan Checklist

This checklist captures the agreed adjustment scope before implementation changes begin.
It is execution-ordered and links code, test, and documentation synchronization tasks.

## Clarified Decisions (Locked)

- [x] Currency validation target: accept any valid ISO-4217 currency code.
- [x] Keep mock verification timeout behavior as HTTP `408` response from the mock endpoint.
- [x] Keep resilience implementation in `AddStandardResilienceHandler`; do not add manual retry loops in `PartnerVerifierClient`.
  - Reference: `docs/analysis/resilience_retry_timeout_guide.md`
- [x] Distinguish upstream timeout failures from upstream service-availability failures.
- [x] API boundary status mapping:
  - timeout category -> `408 Request Timeout`
  - service-unavailable category -> `503 Service Unavailable`
- [x] Use common, reusable exception names (not partner-specific names).
- [x] Add explicit coverage reporting command/output and document it.
- [x] Include documentation and artifact synchronization in the same adjustment scope.

---

## Phase A - Exception Model and Mapping

- [x] Add common exceptions in `src/TransactionValidation.Core/Exceptions`:
  - `UpstreamTimeoutException`
  - `UpstreamServiceUnavailableException`
- [x] Update `src/TransactionValidation.Integration/PartnerVerifierClient.cs` status/exception mapping:
  - `404` -> `NotFoundException`
  - `408` -> `UpstreamTimeoutException`
  - `503` and other `5xx` -> `UpstreamServiceUnavailableException`
  - client timeout/cancellation path (timeout semantics) -> `UpstreamTimeoutException`
- [x] Update `src/TransactionValidation.Configuration/Middleware/ApiExceptionHandler.cs` mappings:
  - `UpstreamTimeoutException` -> `408`
  - `UpstreamServiceUnavailableException` -> `503`
- [x] Keep existing mappings (`400`, `401`, `404`, `409`, fallback `500`) intact.

## Phase B - Currency Validation Upgrade

- [x] Replace fixed allowlist in `src/TransactionValidation.Core/Validation/PartnerTransactionRequestValidator.cs` with ISO-4217 validation logic.
- [x] Ensure validation message still returns clear RFC 7807-friendly text.
- [x] Add/update unit tests for valid and invalid ISO currency inputs.

## Phase C - Unit and Integration Test Expansion

- [x] Extend unit tests for `PartnerVerifierClient`:
  - `408` response -> `UpstreamTimeoutException`
  - `503` response -> `UpstreamServiceUnavailableException`
  - `404` response -> `NotFoundException` (regression guard)
- [x] Extend unit tests for `ApiExceptionHandler`:
  - `UpstreamTimeoutException` -> `408`
  - `UpstreamServiceUnavailableException` -> `503`
- [x] Add retry-evidence tests (behavioral proof, not manual loops):
  - verify transient failures are retried via resilience pipeline
  - verify terminal failure after retry budget is exhausted
- [x] Re-run unit and integration suites after changes.

## Phase D - Coverage Reporting

- [x] Add/confirm a repeatable coverage command in docs and/or tasks (for example `dotnet test` with coverage collection).
- [x] Document where coverage artifacts are generated and how to inspect them.
- [x] Capture a coverage run result as evidence for assignment quality.

## Phase E - Documentation and Artifact Sync

- [x] Update `README.md` to align sequence and behavior wording with current idempotency and upstream failure semantics.
- [x] Update implementation docs under `docs/implementation` to reflect the new exception/status model.
- [x] Update architecture docs under `docs/architecture_design` where status semantics are described.
- [x] Regenerate and sync integration summary/report artifacts to remove stale `409 duplicate` wording.
- [x] Verify links in `docs/README.md` and related indexes remain correct.

## Phase F - Final Validation

- [x] Run full unit tests.
- [x] Run integration tests.
- [x] Regenerate report outputs used as assignment evidence.
- [x] Produce final requirement-to-implementation traceability summary for review.

---

## Out of Scope (for this adjustment batch)

- Branching/commit strategy decisions.
- CI environment provisioning changes beyond test/coverage command updates.
- Re-architecture of resilience stack (current library approach remains).
