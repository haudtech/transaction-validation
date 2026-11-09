using FluentAssertions;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Gateway.Contracts;

using Xunit;

using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace TransactionValidation.Configuration.Tests;

public class BackendAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenGatewayCredentialAndClientIdAreValid_InvokesNext()
    {
        var called = false;
        var middleware = new BackendAuthenticationMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = "gateway-secret";
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = "client-123";

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.Gateway }),
            MicrosoftOptions.Create(new ApiKeyOptions()),
            new StubGatewayCredentialVerifier(isValid: true));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenGatewayCredentialIsInvalid_ReturnsUnauthorizedWithoutInvokingNext()
    {
        var called = false;
        var middleware = new BackendAuthenticationMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = "wrong";
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = "client-123";

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.Gateway }),
            MicrosoftOptions.Create(new ApiKeyOptions()),
            new StubGatewayCredentialVerifier(isValid: false));

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("client id")]
    [InlineData("client\tid")]
    public async Task InvokeAsync_WhenClientIdIsMalformed_ReturnsUnauthorized(string clientId)
    {
        var called = false;
        var middleware = new BackendAuthenticationMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = "gateway-secret";
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = clientId;

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.Gateway }),
            MicrosoftOptions.Create(new ApiKeyOptions()),
            new StubGatewayCredentialVerifier(isValid: true));

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenDirectApiKeyModeHasValidKey_InvokesNext()
    {
        var called = false;
        var middleware = new BackendAuthenticationMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "direct-test-key";

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.DirectApiKey }),
            MicrosoftOptions.Create(new ApiKeyOptions { ApiKey = "direct-test-key" }),
            new StubGatewayCredentialVerifier(isValid: false));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenHealthCheckHasNoCredentials_InvokesNext()
    {
        var called = false;
        var middleware = new BackendAuthenticationMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/healthz";

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.Gateway }),
            MicrosoftOptions.Create(new ApiKeyOptions()),
            new StubGatewayCredentialVerifier(isValid: false));

        called.Should().BeTrue();
    }

    private sealed class StubGatewayCredentialVerifier(bool isValid) : IGatewayCredentialVerifier
    {
        public bool IsValid(string credential) => isValid;
    }
}
