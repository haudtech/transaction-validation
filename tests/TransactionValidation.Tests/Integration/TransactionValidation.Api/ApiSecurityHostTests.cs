using System.Net;
using System.Net.Http.Json;

using TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api;

/// <summary>
/// Verifies API key middleware behavior at host level for transaction submission requests.
/// </summary>
public sealed class ApiSecurityHostTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host returns 401 when X-API-Key header is missing")]
    public async Task PostTransactions_WhenApiKeyMissing_ReturnsUnauthorized()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();

        var payload = RequestFactory.CreateValidRequest("tx-missing-key");

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host returns 202 when valid X-API-Key is provided")]
    public async Task PostTransactions_WhenApiKeyPresent_ReturnsAccepted()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var payload = RequestFactory.CreateValidRequest("tx-accepted");

        var response = await client.PostAsJsonAsync("/api/v1/partner/transactions", payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
