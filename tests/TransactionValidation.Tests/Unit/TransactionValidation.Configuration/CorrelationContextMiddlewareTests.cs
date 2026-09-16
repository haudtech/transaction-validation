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
    /// <summary>
    /// Scenario: no correlation header is supplied and a trace identifier exists.
    /// Expected: the trace identifier is used, tagged, and the next delegate runs.
    /// </summary>
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

    /// <summary>
    /// Scenario: no correlation header or trace identifier is available.
    /// Expected: a valid compact GUID correlation identifier is generated.
    /// </summary>
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

    /// <summary>
    /// Scenario: a correlation header contains surrounding whitespace.
    /// Expected: the trimmed header value is stored as the correlation identifier.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHeaderIsProvided_UsesTrimmedHeader()
    {
        var middleware = new CorrelationContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["Correlation-Id"] = "  req-correlation-1  ";

        await middleware.InvokeAsync(context);

        context.Items[CorrelationContextMiddleware.CorrelationIdItemKey].Should().Be("req-correlation-1");
    }

    /// <summary>
    /// Scenario: the correlation header exceeds the maximum length.
    /// Expected: a bad-request response is returned and the next delegate is skipped.
    /// </summary>
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

    /// <summary>
    /// Scenario: the correlation header contains a control character.
    /// Expected: the request is rejected with a bad-request response.
    /// </summary>
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
