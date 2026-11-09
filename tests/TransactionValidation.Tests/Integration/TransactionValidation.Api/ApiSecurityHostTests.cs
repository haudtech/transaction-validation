using System.Net;
using System.Net.Http.Json;

using TransactionValidation.Gateway.Contracts;
using TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api;

public sealed class ApiSecurityHostTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host rejects gateway requests without a gateway credential")]
    public async Task PostTransactions_WhenGatewayCredentialIsMissing_ReturnsUnauthorizedWithoutPublishing()
    {
        var publisher = new RecordingMessagePublisher();
        using var factory = new ApiHostTestFactory(messagePublisher: publisher, useGatewayAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.AuthenticatedClientId, "client-123");

        var response = await client.PostAsJsonAsync(
            "/api/v1/partner/transactions",
            RequestFactory.CreateValidRequest("tx-missing-gateway-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, publisher.PublishCount);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host rejects invalid gateway credentials without publishing")]
    public async Task PostTransactions_WhenGatewayCredentialIsInvalid_ReturnsUnauthorizedWithoutPublishing()
    {
        var publisher = new RecordingMessagePublisher();
        using var factory = new ApiHostTestFactory(messagePublisher: publisher, useGatewayAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.GatewayKey, "wrong-gateway-key");
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.AuthenticatedClientId, "client-123");

        var response = await client.PostAsJsonAsync(
            "/api/v1/partner/transactions",
            RequestFactory.CreateValidRequest("tx-invalid-gateway-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, publisher.PublishCount);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host rejects gateway requests without a trusted client ID")]
    public async Task PostTransactions_WhenClientIdIsMissing_ReturnsUnauthorizedWithoutPublishing()
    {
        var publisher = new RecordingMessagePublisher();
        using var factory = new ApiHostTestFactory(messagePublisher: publisher, useGatewayAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.GatewayKey, ApiHostTestFactory.GatewayKey);

        var response = await client.PostAsJsonAsync(
            "/api/v1/partner/transactions",
            RequestFactory.CreateValidRequest("tx-missing-client-id"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, publisher.PublishCount);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Security")]
    [Fact(DisplayName = "API host accepts valid gateway credentials and client identity")]
    public async Task PostTransactions_WhenGatewayCredentialAndClientIdAreValid_ReturnsAccepted()
    {
        var publisher = new RecordingMessagePublisher();
        using var factory = new ApiHostTestFactory(messagePublisher: publisher, useGatewayAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.GatewayKey, ApiHostTestFactory.GatewayKey);
        client.DefaultRequestHeaders.Add(GatewayAuthenticationHeaders.AuthenticatedClientId, "client-123");

        var response = await client.PostAsJsonAsync(
            "/api/v1/partner/transactions",
            RequestFactory.CreateValidRequest("tx-gateway-accepted"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, publisher.PublishCount);
    }
}
