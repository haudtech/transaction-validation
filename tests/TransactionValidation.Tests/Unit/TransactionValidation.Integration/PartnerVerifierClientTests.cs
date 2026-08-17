#nullable enable

using System.Net;
using System.Net.Http;
using FluentAssertions;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Integration;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Integration;

/// <summary>
/// Validates partner verifier request behavior and status-to-exception mapping semantics.
/// </summary>
public sealed class PartnerVerifierClientTests
{
    [Fact]
    public async Task VerifyAsync_WhenPartnerIdIsEmpty_ThrowsBadRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync(string.Empty, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsSuccess_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var result = await sut.VerifyAsync("partner-123", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsFailure_ThrowsNotFoundException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync("missing-partner", CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsRequestTimeout_ThrowsUpstreamTimeoutException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.RequestTimeout));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync("partner-timeout", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamTimeoutException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsServiceUnavailable_ThrowsUpstreamServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync("partner-unavailable", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenForceTimeoutProvided_AppendsForceTimeoutQuery()
    {
        Uri? capturedRequestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        await sut.VerifyAsync("partner-123", CancellationToken.None, true);

        capturedRequestUri.Should().NotBeNull();
        capturedRequestUri!.Query.Should().Contain("forceTimeout=true");
    }

    [Fact]
    public async Task VerifyAsync_WhenCancellationRequested_PropagatesTaskCanceledException()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await sut.VerifyAsync("partner-123", cts.Token);

        await action.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenRequestTimesOutWithoutExternalCancellation_ThrowsUpstreamTimeoutException()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("request timed out"));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync("partner-timeout", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamTimeoutException>();
    }

    [Fact]
    public async Task VerifyAsync_WhenHttpRequestExceptionThrown_ThrowsUpstreamServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection failed"));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient);

        var action = async () => await sut.VerifyAsync("partner-unavailable", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>();
    }

    /// <summary>
    /// Provides deterministic HTTP responses and exception simulation for verifier tests.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = (request, _) => Task.FromResult(responseFactory(request));
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseFactory(request, cancellationToken);
        }
    }
}
