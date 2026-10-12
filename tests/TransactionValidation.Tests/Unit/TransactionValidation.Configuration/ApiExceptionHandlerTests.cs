using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Core.Exceptions;

using Xunit;

namespace TransactionValidation.Configuration.Tests;

/// <summary>
/// Verifies domain exception to ProblemDetails status-code mappings.
/// </summary>
public class ApiExceptionHandlerTests
{
    /// <summary>
    /// Scenario: a bad-request domain exception is handled.
    /// Expected: HTTP 400 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenBadRequestException_Maps400ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new BadRequestException("invalid payload"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");
        (await ReadDetailAsync(context)).Should().Be("invalid payload");
    }

    /// <summary>
    /// Scenario: a not-found domain exception is handled.
    /// Expected: HTTP 404 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenNotFoundException_Maps404ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new NotFoundException("missing"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await ReadDetailAsync(context)).Should().Be("missing");
    }

    /// <summary>
    /// Scenario: an upstream timeout exception is handled.
    /// Expected: HTTP 408 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenUpstreamTimeoutException_Maps408ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new UpstreamTimeoutException("partner verification timed out"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status408RequestTimeout);
        (await ReadDetailAsync(context)).Should().Be("partner verification timed out");
    }

    /// <summary>
    /// Scenario: an upstream service-unavailable exception is handled.
    /// Expected: HTTP 503 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenUpstreamServiceUnavailableException_Maps503ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new UpstreamServiceUnavailableException("partner verification unavailable"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        (await ReadDetailAsync(context)).Should().Be("partner verification unavailable");
    }

    /// <summary>
    /// Scenario: a conflict exception is handled.
    /// Expected: HTTP 409 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenConflictException_Maps409ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new ConflictException("duplicate"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        (await ReadDetailAsync(context)).Should().Be("duplicate");
    }

    /// <summary>
    /// Scenario: an unauthorized exception is handled.
    /// Expected: HTTP 401 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenUnauthorizedException_Maps401ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new UnauthorizedAccessException("invalid key"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        (await ReadDetailAsync(context)).Should().Be("invalid key");
    }

    /// <summary>
    /// Scenario: an unrecognized exception is handled.
    /// Expected: HTTP 500 ProblemDetails is written.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsUnknown_Maps500ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = CreateHttpContext();

        var result = await handler.TryHandleAsync(context, new InvalidOperationException("unexpected"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await ReadDetailAsync(context)).Should().Be("An unexpected error occurred.");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string?> ReadDetailAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("detail").GetString();
    }
}
