using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Models;
using TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api;

/// <summary>
/// Verifies host-level ProblemDetails mappings for domain and infrastructure exception paths.
/// </summary>
public sealed class ApiExceptionMappingHostTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "ExceptionMapping")]
    [Fact(DisplayName = "API host maps invalid request payload to 400 ProblemDetails")]
    public async Task PostTransactions_WhenRequestInvalid_ReturnsBadRequestProblemDetails()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var valid = RequestFactory.CreateValidRequest("tx-invalid");
        var payload = new PartnerTransactionRequest
        {
            PartnerId = valid.PartnerId,
            TransactionReference = valid.TransactionReference,
            Amount = valid.Amount,
            Currency = "XXX",
            Timestamp = valid.Timestamp
        };

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bad Request", problem?.Title);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "ExceptionMapping")]
    [Fact(DisplayName = "API host maps NotFoundException from verifier to 404 ProblemDetails")]
    public async Task PostTransactions_WhenVerifierThrowsNotFound_ReturnsNotFoundProblemDetails()
    {
        using var factory = new ApiHostTestFactory(partnerVerifier: new ThrowingPartnerVerifier(new NotFoundException("partner not found")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", RequestFactory.CreateValidRequest("tx-not-found"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Not Found", problem?.Title);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "ExceptionMapping")]
    [Fact(DisplayName = "API host maps ConflictException from publisher to 409 ProblemDetails")]
    public async Task PostTransactions_WhenPublisherThrowsConflict_ReturnsConflictProblemDetails()
    {
        using var factory = new ApiHostTestFactory(messagePublisher: new ThrowingMessagePublisher(new ConflictException("publish confirm failed")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", RequestFactory.CreateValidRequest("tx-conflict"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Conflict", problem?.Title);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "ExceptionMapping")]
    [Fact(DisplayName = "API host maps UnauthorizedAccessException from verifier to 401 ProblemDetails")]
    public async Task PostTransactions_WhenVerifierThrowsUnauthorizedAccess_ReturnsUnauthorizedProblemDetails()
    {
        using var factory = new ApiHostTestFactory(partnerVerifier: new ThrowingPartnerVerifier(new UnauthorizedAccessException("partner unauthorized")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", RequestFactory.CreateValidRequest("tx-unauthorized"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unauthorized", problem?.Title);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "ExceptionMapping")]
    [Fact(DisplayName = "API host maps unhandled exceptions to 500 ProblemDetails")]
    public async Task PostTransactions_WhenUnhandledExceptionThrown_ReturnsInternalServerErrorProblemDetails()
    {
        using var factory = new ApiHostTestFactory(messagePublisher: new ThrowingMessagePublisher(new InvalidOperationException("unexpected failure")));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", RequestFactory.CreateValidRequest("tx-500"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Internal Server Error", problem?.Title);
    }
}
