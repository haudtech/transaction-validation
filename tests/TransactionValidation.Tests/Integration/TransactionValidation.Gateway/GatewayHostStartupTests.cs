using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using TransactionValidation.Gateway;
using TransactionValidation.Gateway.Management;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayHostStartupTests
{
    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway host starts and exposes health endpoint")]
    public async Task GetHealth_WhenGatewayHostStarts_ReturnsSuccess()
    {
        using var factory = new WebApplicationFactory<GatewayEntryPoint>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway host returns not found for paths outside configured routes")]
    public async Task GetUnknownRoute_WhenNoRouteMatches_ReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<GatewayEntryPoint>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/not-a-gateway-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Management host starts and exposes health endpoint")]
    public async Task GetHealth_WhenManagementHostStarts_ReturnsSuccess()
    {
        using var factory = new WebApplicationFactory<ManagementEntryPoint>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
