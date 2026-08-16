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
    [Fact(DisplayName = "API host replays 202 response on second request when same Idempotency-Key is reused")]
    public async Task PostTransactions_WhenIdempotencyKeyReused_ReturnsSameAcceptedResponseOnSecondRequest()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "same-idempotency-key");

        var payload = RequestFactory.CreateValidRequest("tx-duplicate");

        var firstResponse = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);
        var secondResponse = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<AcceptedResponseDto>();
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<AcceptedResponseDto>();

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
        Assert.Equal(firstBody?.MessageId, secondBody?.MessageId);
        Assert.Equal(firstBody?.CorrelationId, secondBody?.CorrelationId);
        Assert.Equal("accepted", secondBody?.Status);
    }

    private sealed class AcceptedResponseDto
    {
        public string MessageId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}
