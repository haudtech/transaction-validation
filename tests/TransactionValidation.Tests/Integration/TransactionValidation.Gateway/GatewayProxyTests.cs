using System.Collections.Concurrent;
using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TransactionValidation.Gateway.Contracts.Models;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayProxyTests
{
    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway rewrites the transaction path and preserves query and backend response")]
    public async Task PostTransaction_WhenRouteMatches_ProxiesRequestAndResponse()
    {
        await using var backend = new TestBackend();
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address);
        using var client = CreateAuthenticatedClient(factory);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/transactions/v1/partner/transactions?source=test")
        {
            Content = new StringContent("{\"transactionId\":\"tx-123\"}")
        };
        request.Headers.Add("X-API-Key", "spoofed-client-key");
        request.Headers.Add(GatewayAuthenticationHeaders.GatewayKey, "spoofed-backend-key");
        request.Headers.Add(GatewayAuthenticationHeaders.AuthenticatedClientId, "spoofed-client");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");
        request.Headers.Add("X-Real-IP", "192.0.2.10");
        request.Headers.Add("Cookie", "session=spoofed");
        request.Headers.Add("Correlation-Id", "  client-correlation-01  ");
        request.Headers.Add("Idempotency-Key", "transaction-key-01");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("backend-result", response.Headers.GetValues("X-Backend-Result").Single());
        Assert.Equal("accepted", await response.Content.ReadAsStringAsync());
        Assert.Equal("client-correlation-01", response.Headers.GetValues("Correlation-Id").Single());
        Assert.Equal("/api/v1/partner/transactions?source=test", backend.Requests.Single());
        Assert.Equal("{\"transactionId\":\"tx-123\"}", backend.RequestBodies.Single());
        Assert.Equal(1, backend.RequestCount);
        var forwardedHeaders = backend.RequestHeaders.Single();
        Assert.Equal("test-client", forwardedHeaders[GatewayAuthenticationHeaders.AuthenticatedClientId]);
        Assert.Equal(GatewayTestCredentials.BackendCredential, forwardedHeaders[GatewayAuthenticationHeaders.GatewayKey]);
        Assert.Equal("client-correlation-01", forwardedHeaders["Correlation-Id"]);
        Assert.Equal("transaction-key-01", forwardedHeaders["Idempotency-Key"]);
        Assert.DoesNotContain("Authorization", forwardedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-API-Key", forwardedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Forwarded-For", forwardedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Real-IP", forwardedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", forwardedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task TransactionRoute_WhenMethodIsNotPost_ReturnsMethodNotAllowed(string method)
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = CreateAuthenticatedClient(factory);

        using var request = new HttpRequestMessage(new HttpMethod(method), "/transactions/v1/partner/transactions");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", response.Content.Headers.Allow.Single());
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway sanitizes backend connection failures")]
    public async Task PostTransaction_WhenBackendIsUnavailable_ReturnsSanitizedBadGateway()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = CreateAuthenticatedClient(factory);

        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/transactions/v1/partner/transactions", "{}");
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain("127.0.0.1", responseBody, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway propagates client cancellation without retrying the backend request")]
    public async Task PostTransaction_WhenClientCancels_AbortsSingleBackendRequest()
    {
        await using var backend = new TestBackend(blockResponse: true);
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address);
        using var client = CreateAuthenticatedClient(factory);
        using var cancellation = new CancellationTokenSource();

        var request = client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Post, "/transactions/v1/partner/transactions", "{}"),
            cancellation.Token);

        await backend.RequestReceived.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
        Assert.Equal(1, backend.RequestCount);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway maps backend activity timeouts to a sanitized gateway timeout")]
    public async Task PostTransaction_WhenBackendExceedsActivityTimeout_ReturnsSanitizedGatewayTimeout()
    {
        await using var backend = new TestBackend(blockResponse: true);
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address, activityTimeout: "00:00:00.100");
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Post, "/transactions/v1/partner/transactions", "{}"));
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.DoesNotContain(backend.Address, responseBody, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact(DisplayName = "Gateway returns not found for paths outside configured routes")]
    public async Task GetUnknownRoute_WhenNoRouteMatches_ReturnsNotFound()
    {
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/");
        using var client = CreateAuthenticatedClient(factory);

        using var response = await client.GetAsync("/not-a-gateway-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(GatewayHostFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            System.Net.Http.Headers.AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);
        return client;
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string uri,
        string? content = null)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = content is null ? null : new StringContent(content)
        };
        request.Headers.Authorization =
            System.Net.Http.Headers.AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);
        return request;
    }

    private sealed class TestBackend : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly ConcurrentQueue<string> _requestBodies = new();
        private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _requestHeaders = new();
        private readonly bool _blockResponse;
        private readonly TaskCompletionSource _requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public TestBackend(bool blockResponse = false)
        {
            _blockResponse = blockResponse;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            _app = builder.Build();
            _app.Run(async context =>
            {
                Interlocked.Increment(ref _requestCount);
                _requests.Enqueue($"{context.Request.Path}{context.Request.QueryString}");
                _requestHeaders.Enqueue(context.Request.Headers.ToDictionary(
                    header => header.Key,
                    header => header.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase));
                using var reader = new StreamReader(context.Request.Body);
                _requestBodies.Enqueue(await reader.ReadToEndAsync(context.RequestAborted));
                _requestReceived.TrySetResult();
                if (_blockResponse)
                {
                    await Task.Delay(Timeout.Infinite, context.RequestAborted);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status202Accepted;
                context.Response.Headers["X-Backend-Result"] = "backend-result";
                await context.Response.WriteAsync("accepted");
            });
        }

        public string Address { get; private set; } = string.Empty;

        public int RequestCount => Volatile.Read(ref _requestCount);

        public IReadOnlyCollection<string> Requests => _requests.ToArray();

        public IReadOnlyCollection<string> RequestBodies => _requestBodies.ToArray();

        public IReadOnlyCollection<IReadOnlyDictionary<string, string>> RequestHeaders => _requestHeaders.ToArray();

        public Task RequestReceived => _requestReceived.Task;

        public async Task StartAsync()
        {
            await _app.StartAsync();
            var server = _app.Services.GetRequiredService<IServer>();
            Address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
