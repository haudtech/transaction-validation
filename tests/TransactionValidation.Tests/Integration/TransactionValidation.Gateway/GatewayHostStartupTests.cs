using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TransactionValidation.Gateway.Management.Hosting;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayHostStartupTests
{
    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway host starts and exposes health endpoint")]
    public async Task GetHealth_WhenGatewayHostStarts_ReturnsSuccess()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway host returns not found for paths outside configured routes")]
    public async Task GetUnknownRoute_WhenNoRouteMatches_ReturnsNotFound()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/not-a-gateway-route");
        request.Headers.Authorization =
            System.Net.Http.Headers.AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Management host starts and exposes health endpoint")]
    public async Task GetHealth_WhenManagementHostStarts_ReturnsSuccess()
    {
        using var factory = new ManagementHostFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Management credential endpoints require authentication")]
    public async Task GetBackendCredentials_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new ManagementHostFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/admin/backend-credentials/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Management read and write scopes are enforced independently")]
    public async Task ManagementEndpoints_WhenCallerHasWrongScope_ReturnsForbidden()
    {
        using var factory = new ManagementHostFactory();
        using var client = factory.CreateClient();
        using var readRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/backend-credentials/");
        readRequest.Headers.Add("X-Test-Scopes", "gateway.write");
        using var readResponse = await client.SendAsync(readRequest);
        using var writeRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/backend-credentials/");
        writeRequest.Headers.Add("X-Test-Scopes", "gateway.read");
        using var writeResponse = await client.SendAsync(writeRequest);

        Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }

    private sealed class ManagementHostFactory : WebApplicationFactory<ManagementEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GatewayPolicy"] = "Host=127.0.0.1;Database=gateway_test;Username=test",
                    ["Management:Oidc:Authority"] = "https://issuer.example",
                    ["Management:Oidc:Audience"] = "gateway-management",
                    ["Gateway:BackendCredentialManagement:PepperVersion"] = "backend-pepper-v1",
                    ["Secrets:Provider"] = "LocalFile",
                    ["Secrets:Directory"] = ".secrets"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestManagementAuthenticationHandler>("Test", _ => { });
            });
        }
    }

    private sealed class TestManagementAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Scopes", out var scopeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim("iss", "https://issuer.example"),
                new Claim("sub", "admin-123"),
                new Claim("scope", scopeHeader.ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
