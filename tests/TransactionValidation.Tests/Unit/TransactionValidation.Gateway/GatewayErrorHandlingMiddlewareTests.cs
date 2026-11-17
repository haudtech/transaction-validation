using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using TransactionValidation.Gateway.Middleware;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class GatewayErrorHandlingMiddlewareTests
{
    [Theory]
    [InlineData(true, StatusCodes.Status413PayloadTooLarge, "payload_too_large")]
    [InlineData(false, StatusCodes.Status500InternalServerError, "internal_error")]
    public async Task InvokeAsync_WhenRequestFails_ReturnsSanitizedProblem(bool tooLarge, int status, string code)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        context.Items[GatewayCorrelationMiddleware.CorrelationIdItemKey] = "test-correlation";
        var middleware = new GatewayErrorHandlingMiddleware(
            _ => throw (tooLarge
                ? new BadHttpRequestException("private request data", StatusCodes.Status413PayloadTooLarge)
                : new InvalidOperationException("private exception data")),
            NullLogger<GatewayErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.Equal("test-correlation", document.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("private", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
