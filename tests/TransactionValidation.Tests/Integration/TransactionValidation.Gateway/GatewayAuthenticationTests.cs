using System.Net;
using System.Net.Http.Headers;

using TransactionValidation.Gateway.Contracts.Exceptions;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayAuthenticationTests
{
    [Trait("Category", "Integration")]
    [Fact(DisplayName = "All invalid client credentials return the same generic unauthorized response before route lookup")]
    public async Task Request_WhenCredentialIsInvalid_ReturnsEquivalentUnauthorizedResponse()
    {
        var now = DateTimeOffset.UtcNow;
        var credentials = new[]
        {
            GatewayTestCredentials.CreateCredential(keyId: "valid"),
            GatewayTestCredentials.CreateCredential(
                keyId: "expired",
                expiresAt: now.AddSeconds(-1)),
            GatewayTestCredentials.CreateCredential(
                keyId: "revoked",
                status: GatewayClientKeyStatus.Revoked,
                revokedAt: now.AddSeconds(-1)),
            GatewayTestCredentials.CreateCredential(
                keyId: "disabled",
                clientEnabled: false),
            GatewayTestCredentials.CreateCredential(
                keyId: "not-yet-valid",
                notBefore: now.AddMinutes(1)),
            GatewayTestCredentials.CreateCredential(
                keyId: "previous-expired",
                status: GatewayClientKeyStatus.Previous,
                notBefore: now.AddHours(-2),
                expiresAt: now.AddSeconds(-1),
                previousSince: now.AddHours(-1)),
            GatewayTestCredentials.CreateCredential(
                keyId: "previous-too-long",
                status: GatewayClientKeyStatus.Previous,
                notBefore: now.AddHours(-3),
                expiresAt: now.AddHours(1),
                previousSince: now.AddHours(-2)),
            GatewayTestCredentials.CreateCredential(
                keyId: "wrong-verifier",
                verifier: new byte[32])
        };
        using var factory = new GatewayHostFactory(
            "http://127.0.0.1:1/",
            credentialStore: new InMemoryClientCredentialStore(credentials));
        using var client = factory.CreateClient();

        var requests = new[]
        {
            (Authorization: (string?)null, Path: "/not-a-route"),
            (Authorization: "Bearer token", Path: "/transactions/v1/partner/transactions"),
            (Authorization: "ApiKey malformed", Path: "/not-a-route"),
            (Authorization: $"ApiKey unknown.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey valid.{new string('B', 43)}", Path: "/not-a-route"),
            (Authorization: $"ApiKey expired.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey revoked.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey disabled.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey not-yet-valid.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey previous-expired.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey previous-too-long.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route"),
            (Authorization: $"ApiKey wrong-verifier.{GatewayTestCredentials.ClientSecret}", Path: "/not-a-route")
        };
        string? expectedBody = null;
        foreach (var testCase in requests)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, testCase.Path);
            if (testCase.Authorization is not null)
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(testCase.Authorization);
            }

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            expectedBody ??= body;
            Assert.Equal(expectedBody, body);
        }
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Previous client key is accepted only inside its one-hour overlap")]
    public async Task Request_WhenPreviousKeyIsInsideOverlap_ContinuesToRouteResolution()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = GatewayTestCredentials.CreateCredential(
            status: GatewayClientKeyStatus.Previous,
            notBefore: now.AddHours(-2),
            expiresAt: now.AddMinutes(30),
            previousSince: now.AddMinutes(-30));
        using var factory = new GatewayHostFactory(
            "http://127.0.0.1:1/",
            credentialStore: new InMemoryClientCredentialStore([credential]));
        using var client = factory.CreateClient();
        using var request = CreateRequest("/not-a-route", credential.KeyId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Authenticated clients receive forbidden for an ungranted API version")]
    public async Task PostTransaction_WhenClientHasNoRouteGrant_ReturnsForbidden()
    {
        var credential = GatewayTestCredentials.CreateCredential(grants: []);
        using var factory = new GatewayHostFactory(
            "http://127.0.0.1:1/",
            credentialStore: new InMemoryClientCredentialStore([credential]));
        using var client = factory.CreateClient();
        using var request = CreateRequest("/transactions/v1/partner/transactions", credential.KeyId, HttpMethod.Post);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("{\"error\":\"Forbidden\"}", await response.Content.ReadAsStringAsync());
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Authenticated unknown routes return not found")]
    public async Task GetUnknownRoute_WhenClientIsAuthenticated_ReturnsNotFound()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();
        using var request = CreateRequest("/not-a-route", GatewayTestCredentials.KeyId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Credential-store outages fail closed with service unavailable")]
    public async Task Request_WhenCredentialStoreIsUnavailable_ReturnsServiceUnavailable()
    {
        using var factory = new GatewayHostFactory(
            "http://127.0.0.1:1/",
            credentialStore: new UnavailableCredentialStore());
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/not-a-route")
        {
            Headers = { Authorization = AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization) }
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"error\":\"Service unavailable\"}", await response.Content.ReadAsStringAsync());
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Missing backend credential fails closed without proxying")]
    public async Task PostTransaction_WhenBackendCredentialIsUnavailable_ReturnsServiceUnavailable()
    {
        using var factory = new GatewayHostFactory(
            "http://127.0.0.1:1/",
            secretProvider: GatewayTestCredentials.CreateSecretProvider(includeBackendCredential: false));
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/transactions/v1/partner/transactions",
            GatewayTestCredentials.KeyId,
            HttpMethod.Post);
        request.Content = new StringContent("{}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"error\":\"Service unavailable\"}", await response.Content.ReadAsStringAsync());
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Health remains accessible without partner credentials")]
    public async Task GetHealth_WhenCredentialIsMissing_ReturnsSuccess()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Health subpaths are not exempt from client authentication")]
    public async Task GetHealthSubpath_WhenCredentialIsMissing_ReturnsUnauthorized()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/healthz/not-real");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Invalid correlation input is replaced and returned canonically")]
    public async Task PostTransaction_WhenCorrelationIsInvalid_ReturnsGeneratedCorrelationId()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/transactions/v1/partner/transactions",
            GatewayTestCredentials.KeyId,
            HttpMethod.Post);
        request.Content = new StringContent("{}");
        request.Headers.Add("Correlation-Id", new string('c', 129));

        using var response = await client.SendAsync(request);
        var correlationId = response.Headers.GetValues("Correlation-Id").Single();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(32, correlationId.Length);
        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
    }

    private static HttpRequestMessage CreateRequest(string path, string keyId, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(
            $"ApiKey {keyId}.{GatewayTestCredentials.ClientSecret}");
        return request;
    }

    private sealed class UnavailableCredentialStore : IClientCredentialStore
    {
        public Task<ClientCredentialRecord?> GetByKeyIdAsync(string keyId, CancellationToken cancellationToken) =>
            throw new GatewayPolicyStoreUnavailableException(
                "The test policy store is unavailable.",
                new IOException());
    }
}
