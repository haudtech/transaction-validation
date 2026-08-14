using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace TransactionValidation.Configuration.Tests;

public class ApiKeyMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenApiKeyMissing_ReturnsUnauthorized()
    {
        var options = MicrosoftOptions.Create(new ApiKeyOptions { Enabled = true, ApiKey = "abc123", HeaderName = "X-API-Key" });
        var middleware = new ApiKeyMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, options);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

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
}
