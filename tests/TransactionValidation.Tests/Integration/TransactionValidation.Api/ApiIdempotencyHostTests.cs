#nullable enable

using System.Net;
using System.Net.Http.Json;
using TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;
using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api;

public sealed class ApiIdempotencyHostTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "Idempotency")]
    [Fact(DisplayName = "API host returns 409 on second request when same Idempotency-Key is reused")]
    public async Task PostTransactions_WhenIdempotencyKeyReused_ReturnsConflictOnSecondRequest()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "same-idempotency-key");

        var payload = RequestFactory.CreateValidRequest("tx-duplicate");

        var firstResponse = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);
        var secondResponse = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }
}
