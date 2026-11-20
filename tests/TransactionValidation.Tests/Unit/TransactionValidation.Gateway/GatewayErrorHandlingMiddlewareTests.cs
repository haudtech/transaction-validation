using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

    [Fact]
    public async Task InvokeAsync_WhenKestrelRejectsOversizedBody_ReturnsPayloadTooLargeProblem()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16);
        await using var app = builder.Build();
        app.UseMiddleware<GatewayErrorHandlingMiddleware>();
        app.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            await reader.ReadToEndAsync();
        });
        await app.StartAsync();

        using var client = new HttpClient();
        using var response = await client.PostAsync(new Uri(app.Urls.First()), new StringContent(new string('a', 1024)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("payload_too_large", document.RootElement.GetProperty("code").GetString());
        await app.StopAsync();
    }

    [Fact]
    public async Task InvokeAsync_WhenContentLengthExceedsLimit_ReturnsPayloadTooLargeWithoutCallingNext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new MaxBodySizeFeature(16));
        context.Request.ContentLength = 17;
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var called = false;
        var middleware = new GatewayErrorHandlingMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            NullLogger<GatewayErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
    }

    private sealed class MaxBodySizeFeature(long? limit) : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; } = limit;
    }
}
