using System.Net;
using System.Net.Http.Json;

using TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api;

public sealed class ApiStartupHostTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "Startup")]
    [Fact(DisplayName = "API host starts with Azure Service Bus broker selection")]
    public async Task PostTransactions_WhenAzureBrokerIsSelected_ReturnsAccepted()
    {
        using var factory = new ApiHostTestFactory(useAzureServiceBus: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.PostAsJsonAsync(
            "/api/v1/partner/transactions",
            RequestFactory.CreateValidRequest("tx-azure-startup"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Startup")]
    [Fact(DisplayName = "API host exposes Swagger in Development")]
    public async Task GetSwagger_WhenEnvironmentIsDevelopment_ReturnsSuccess()
    {
        using var factory = new ApiHostTestFactory(environmentName: "Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiHostTestFactory.ApiKey);

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "Startup")]
    [Fact(DisplayName = "API host exposes health endpoint without API key")]
    public async Task GetHealth_WhenApiKeyIsMissing_ReturnsSuccess()
    {
        using var factory = new ApiHostTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
