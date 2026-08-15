# Integration Test Summary

- Source TRX: `TestResults/integration/integration-tests.trx`
- Generated: `2026-08-15 23:15:02 +07:00`

## Overall

| Total | Passed | Failed | Skipped | Pass Rate |
|---:|---:|---:|---:|---:|
| 11 | 11 | 0 | 0 | 100.00% |

## By Traits

| Category | Feature | Total | Passed | Failed | Skipped | Pass Rate |
|---|---|---:|---:|---:|---:|---:|
| Integration | ExceptionMapping | 5 | 5 | 0 | 0 | 100.00% |
| Integration | Idempotency | 1 | 1 | 0 | 0 | 100.00% |
| Integration | MockVerification | 3 | 3 | 0 | 0 | 100.00% |
| Integration | Security | 2 | 2 | 0 | 0 | 100.00% |

## Integration Test Details

| Full Description | Category | Feature | Outcome | Duration | Class | Method |
|---|---|---|---|---:|---|---|
| API host maps ConflictException from publisher to 409 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:00.0499460 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenPublisherThrowsConflict_ReturnsConflictProblemDetails |
| API host maps invalid request payload to 400 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:00.0465974 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenRequestInvalid_ReturnsBadRequestProblemDetails |
| API host maps NotFoundException from verifier to 404 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:00.5077093 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenVerifierThrowsNotFound_ReturnsNotFoundProblemDetails |
| API host maps UnauthorizedAccessException from verifier to 401 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:00.0422015 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenVerifierThrowsUnauthorizedAccess_ReturnsUnauthorizedProblemDetails |
| API host maps unhandled exceptions to 500 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:00.0473753 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenUnhandledExceptionThrown_ReturnsInternalServerErrorProblemDetails |
| API host returns 409 on second request when same Idempotency-Key is reused | Integration | Idempotency | Passed | 00:00:00.5069166 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiIdempotencyHostTests | PostTransactions_WhenIdempotencyKeyReused_ReturnsConflictOnSecondRequest |
| Mock VerifyPartner random path produces timeout rate near 30% | Integration | MockVerification | Passed | 00:00:00.0019336 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForceTimeoutNotProvided_TimeoutRateIsApproximatelyThirtyPercent |
| Mock VerifyPartner returns 200 with verified=true when forceTimeout=false | Integration | MockVerification | Passed | 00:00:00.0158362 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForcedSuccess_Returns200VerifiedTrue |
| Mock VerifyPartner returns 408 with timeout payload when forceTimeout=true | Integration | MockVerification | Passed | 00:00:00.0002536 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForcedTimeout_Returns408WithTimeoutPayload |
| API host returns 202 when valid X-API-Key is provided | Integration | Security | Passed | 00:00:00.4819158 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiSecurityHostTests | PostTransactions_WhenApiKeyPresent_ReturnsAccepted |
| API host returns 401 when X-API-Key header is missing | Integration | Security | Passed | 00:00:00.0590625 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiSecurityHostTests | PostTransactions_WhenApiKeyMissing_ReturnsUnauthorized |

## Failed Tests

- None
