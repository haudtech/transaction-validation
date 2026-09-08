# Integration Test Summary

- Source TRX: `TestResults/integration/integration-tests.trx`
- Generated: `2026-09-08 19:24:20 +07:00`

## Overall

| Total | Passed | Failed | Skipped | Pass Rate |
|---:|---:|---:|---:|---:|
| 14 | 14 | 0 | 0 | 100.00% |

## By Traits

| Category | Feature | Total | Passed | Failed | Skipped | Pass Rate |
|---|---|---:|---:|---:|---:|---:|
| Integration | ExceptionMapping | 5 | 5 | 0 | 0 | 100.00% |
| Integration | Idempotency | 1 | 1 | 0 | 0 | 100.00% |
| Integration | MockVerification | 3 | 3 | 0 | 0 | 100.00% |
| Integration | Security | 2 | 2 | 0 | 0 | 100.00% |
| Integration | Startup | 3 | 3 | 0 | 0 | 100.00% |

## Integration Test Details

| Full Description | Category | Feature | Outcome | Duration | Class | Method |
|---|---|---|---|---:|---|---|
| API host maps ConflictException from publisher to 409 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:04.6138870 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenPublisherThrowsConflict_ReturnsConflictProblemDetails |
| API host maps invalid request payload to 400 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:04.7016205 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenRequestInvalid_ReturnsBadRequestProblemDetails |
| API host maps NotFoundException from verifier to 404 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:05.2201807 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenVerifierThrowsNotFound_ReturnsNotFoundProblemDetails |
| API host maps UnauthorizedAccessException from verifier to 401 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:04.6516107 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenVerifierThrowsUnauthorizedAccess_ReturnsUnauthorizedProblemDetails |
| API host maps unhandled exceptions to 500 ProblemDetails | Integration | ExceptionMapping | Passed | 00:00:04.6351863 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiExceptionMappingHostTests | PostTransactions_WhenUnhandledExceptionThrown_ReturnsInternalServerErrorProblemDetails |
| API host replays 202 response on second request when same Idempotency-Key is reused | Integration | Idempotency | Passed | 00:00:05.2187157 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiIdempotencyHostTests | PostTransactions_WhenIdempotencyKeyReused_ReturnsSameAcceptedResponseOnSecondRequest |
| Mock VerifyPartner random path produces timeout rate near 30% | Integration | MockVerification | Passed | 00:00:00.0020491 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForceTimeoutNotProvided_TimeoutRateIsApproximatelyThirtyPercent |
| Mock VerifyPartner returns 200 with verified=true when forceTimeout=false | Integration | MockVerification | Passed | 00:00:00.0204051 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForcedSuccess_Returns200VerifiedTrue |
| Mock VerifyPartner returns 408 with timeout payload when forceTimeout=true | Integration | MockVerification | Passed | 00:00:00.0002708 | TransactionValidation.Tests.Integration.MockPartnerVerificationControllerTests | VerifyPartner_WhenForcedTimeout_Returns408WithTimeoutPayload |
| API host returns 202 when valid X-API-Key is provided | Integration | Security | Passed | 00:00:05.2085367 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiSecurityHostTests | PostTransactions_WhenApiKeyPresent_ReturnsAccepted |
| API host returns 401 when X-API-Key header is missing | Integration | Security | Passed | 00:00:04.6201263 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiSecurityHostTests | PostTransactions_WhenApiKeyMissing_ReturnsUnauthorized |
| API host exposes health endpoint without API key | Integration | Startup | Passed | 00:00:05.1599356 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiStartupHostTests | GetHealth_WhenApiKeyIsMissing_ReturnsSuccess |
| API host exposes Swagger in Development | Integration | Startup | Passed | 00:00:04.6630093 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiStartupHostTests | GetSwagger_WhenEnvironmentIsDevelopment_ReturnsSuccess |
| API host starts with Azure Service Bus broker selection | Integration | Startup | Passed | 00:00:04.6438965 | TransactionValidation.Tests.Integration.TransactionValidation.Api.ApiStartupHostTests | PostTransactions_WhenAzureBrokerIsSelected_ReturnsAccepted |

## Failed Tests

- None
