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
    [Fact]
    public async Task TryHandleAsync_WhenBadRequestException_Maps400ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = new DefaultHttpContext();

        var result = await handler.TryHandleAsync(context, new BadRequestException("invalid payload"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task TryHandleAsync_WhenNotFoundException_Maps404ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = new DefaultHttpContext();

        var result = await handler.TryHandleAsync(context, new NotFoundException("missing"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TryHandleAsync_WhenUpstreamTimeoutException_Maps408ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = new DefaultHttpContext();

        var result = await handler.TryHandleAsync(context, new UpstreamTimeoutException("partner verification timed out"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status408RequestTimeout);
    }

    [Fact]
    public async Task TryHandleAsync_WhenUpstreamServiceUnavailableException_Maps503ProblemDetails()
    {
        var handler = new ApiExceptionHandler();
        var context = new DefaultHttpContext();

        var result = await handler.TryHandleAsync(context, new UpstreamServiceUnavailableException("partner verification unavailable"), CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
