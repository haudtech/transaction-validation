using System.Diagnostics;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Configuration.Middleware;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

public sealed class CorrelationContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_UsesTraceIdentifierAndInvokesNext()
    {
        var called = false;
        var middleware = new CorrelationContextMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-123";

        using var activity = new Activity("test").Start();

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
        context.Items[CorrelationContextMiddleware.CorrelationIdItemKey].Should().Be("trace-123");
        activity.GetTagItem("app.correlation_id").Should().Be("trace-123");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissingAndTraceIdentifierBlank_GeneratesGuidCorrelationId()
    {
        var middleware = new CorrelationContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.TraceIdentifier = string.Empty;

        await middleware.InvokeAsync(context);

        var correlationId = context.Items[CorrelationContextMiddleware.CorrelationIdItemKey].Should().BeOfType<string>().Subject;
        Guid.TryParseExact(correlationId, "N", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderIsProvided_UsesTrimmedHeader()
    {
        var middleware = new CorrelationContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["Correlation-Id"] = "  req-correlation-1  ";

        await middleware.InvokeAsync(context);

        context.Items[CorrelationContextMiddleware.CorrelationIdItemKey].Should().Be("req-correlation-1");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderIsTooLong_ReturnsBadRequestAndSkipsNext()
    {
        var called = false;
        var middleware = new CorrelationContextMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Correlation-Id"] = new string('a', 129);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        payload.Should().Contain("Correlation-Id header is invalid.");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderContainsControlCharacter_ReturnsBadRequest()
    {
        var middleware = new CorrelationContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Correlation-Id"] = "abc\u0001def";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
