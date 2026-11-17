using System.Net;
using System.Text.Json;

using TransactionValidation.Gateway.Contracts.Models;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayRateLimitTests
{
    private const string Route = "/transactions/v1/partner/transactions";

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway returns 429 with Retry-After when the burst is exhausted and does not forward")]
    public async Task PostTransaction_WhenRateLimitExhausted_Returns429WithRetryAfter()
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        using var factory = CreateFactory(backend, new ClientApiLimit("transaction-validation", 60, 2, 1000, 10_000));
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync(Route, new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync(Route, new StringContent("{}"))).StatusCode);
        using var limited = await client.PostAsync(Route, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("7", limited.Headers.GetValues("Retry-After").Single());
        await AssertProblemCodeAsync(limited, "rate_limit_exceeded");
        Assert.Equal(2, backend.RequestCount);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway returns 429 Quota exceeded when the daily quota is exhausted")]
    public async Task PostTransaction_WhenDailyQuotaExhausted_Returns429Quota()
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        using var factory = CreateFactory(backend, new ClientApiLimit("transaction-validation", 60, 10, 1, 10));
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync(Route, new StringContent("{}"))).StatusCode);
        using var limited = await client.PostAsync(Route, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("10800", limited.Headers.GetValues("Retry-After").Single());
        await AssertProblemCodeAsync(limited, "quota_exceeded");
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway fails closed with 503 when the limiter store is unavailable")]
    public async Task PostTransaction_WhenLimiterUnavailable_Returns503WithoutForwarding()
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address, requestLimiter: new UnavailableClientRequestLimiter());
        using var client = CreateClient(factory);

        using var response = await client.PostAsync(Route, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemCodeAsync(response, "limiter_unavailable");
        Assert.Equal(0, backend.RequestCount);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway fails closed with 503 when a client has no explicit limits")]
    public async Task PostTransaction_WhenClientHasNoLimits_Returns503WithoutForwarding()
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        var store = new InMemoryClientCredentialStore([GatewayTestCredentials.CreateCredential(limits: [])]);
        using var factory = new GatewayHostFactory(backend.Address, credentialStore: store);
        using var client = CreateClient(factory);

        using var response = await client.PostAsync(Route, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemCodeAsync(response, "policy_unavailable");
        Assert.Equal(0, backend.RequestCount);
    }

    private static GatewayHostFactory CreateFactory(GatewayProxyTests.TestBackend backend, ClientApiLimit limit) =>
        new(
            backend.Address,
            credentialStore: new InMemoryClientCredentialStore([GatewayTestCredentials.CreateCredential(limits: [limit])]));

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(response.Headers.GetValues("Correlation-Id").Single(), document.RootElement.GetProperty("correlationId").GetString());
    }

    private static HttpClient CreateClient(GatewayHostFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            System.Net.Http.Headers.AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);
        return client;
    }
}
