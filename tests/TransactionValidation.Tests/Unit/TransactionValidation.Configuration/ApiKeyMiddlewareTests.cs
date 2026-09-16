using FluentAssertions;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;

using Xunit;

using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace TransactionValidation.Configuration.Tests;

/// <summary>
/// Verifies API key middleware authorization behavior for missing and valid headers.
/// </summary>
public class ApiKeyMiddlewareTests
{
    /// <summary>
    /// Scenario: API-key protection is enabled and the request has no key.
    /// Expected: the middleware returns HTTP 401.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenApiKeyMissing_ReturnsUnauthorized()
    {
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = true, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, options);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Scenario: API-key protection is enabled and the request key matches.
    /// Expected: the next middleware is invoked.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenApiKeyMatches_InvokesNext()
    {
        var called = false;
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = true, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "abc123";

        await middleware.InvokeAsync(context, options);

        called.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: API-key protection is disabled.
    /// Expected: the next middleware is invoked without authorization checks.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenApiKeyProtectionIsDisabled_InvokesNext()
    {
        var called = false;
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = false, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, options);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// Scenario: the health endpoint is requested without an API key.
    /// Expected: the health request bypasses API-key validation.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHealthCheckHasNoApiKey_InvokesNext()
    {
        var called = false;
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = true, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = "/healthz";

        await middleware.InvokeAsync(context, options);

        called.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: API-key protection is enabled and the request key is incorrect.
    /// Expected: HTTP 401 is returned and the next middleware is not invoked.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenApiKeyIsWrong_ReturnsUnauthorizedWithoutInvokingNext()
    {
        var called = false;
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = true, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "wrong";

        await middleware.InvokeAsync(context, options);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }
}
