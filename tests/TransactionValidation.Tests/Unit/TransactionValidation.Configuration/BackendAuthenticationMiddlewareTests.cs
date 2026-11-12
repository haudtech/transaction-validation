using FluentAssertions;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Gateway.Contracts.Interfaces;
using TransactionValidation.Gateway.Contracts.Models;

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
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Valid));

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
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Invalid));

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
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Valid));

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
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Invalid));

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
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Invalid));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenCredentialStoreIsUnavailable_ReturnsServiceUnavailable()
    {
        var middleware = new BackendAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[GatewayAuthenticationHeaders.GatewayKey] = "gateway-secret";
        context.Request.Headers[GatewayAuthenticationHeaders.AuthenticatedClientId] = "client-123";

        await middleware.InvokeAsync(
            context,
            MicrosoftOptions.Create(new BackendAuthenticationOptions { AuthenticationMode = BackendAuthenticationMode.Gateway }),
            MicrosoftOptions.Create(new ApiKeyOptions()),
            new StubGatewayCredentialVerifier(GatewayCredentialVerificationResult.Unavailable));

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    private sealed class StubGatewayCredentialVerifier(
        GatewayCredentialVerificationResult result) : IGatewayCredentialVerifier
    {
        public ValueTask<GatewayCredentialVerificationResult> VerifyAsync(
            string credential,
            CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }
}
