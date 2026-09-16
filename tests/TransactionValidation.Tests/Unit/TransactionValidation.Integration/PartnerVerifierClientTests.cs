using System.Net;
using System.Net.Http;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using TransactionValidation.Core.Exceptions;
using TransactionValidation.Integration;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Integration;

/// <summary>
/// Validates partner verifier request behavior and status-to-exception mapping semantics.
/// </summary>
public sealed class PartnerVerifierClientTests
{
    /// <summary>
    /// Scenario: the partner ID is empty.
    /// Expected: verification throws a bad-request exception without calling the partner.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenPartnerIdIsEmpty_ThrowsBadRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync(string.Empty, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns HTTP 200.
    /// Expected: verification returns true.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsSuccess_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var result = await sut.VerifyAsync("partner-123", CancellationToken.None);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns HTTP 404.
    /// Expected: verification throws a not-found exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsFailure_ThrowsNotFoundException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("missing-partner", CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns HTTP 408.
    /// Expected: verification throws an upstream-timeout exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsRequestTimeout_ThrowsUpstreamTimeoutException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.RequestTimeout));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("partner-timeout", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamTimeoutException>();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns HTTP 503.
    /// Expected: verification throws an upstream-service-unavailable exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsServiceUnavailable_ThrowsUpstreamServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("partner-unavailable", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns HTTP 500.
    /// Expected: verification throws an upstream-service-unavailable exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsInternalServerError_ThrowsUpstreamServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("partner-unavailable", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>();
    }

    /// <summary>
    /// Scenario: the partner endpoint returns an unexpected client error.
    /// Expected: verification throws service-unavailable with the status code.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenMockEndpointReturnsUnexpectedClientError_ThrowsServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("partner-invalid", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>()
            .WithMessage("*400*");
    }

    /// <summary>
    /// Scenario: force-timeout mode is enabled.
    /// Expected: the request includes forceTimeout=true.
    /// </summary>
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
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        await sut.VerifyAsync("partner-123", CancellationToken.None, true);

        capturedRequestUri.Should().NotBeNull();
        capturedRequestUri!.Query.Should().Contain("forceTimeout=true");
    }

    /// <summary>
    /// Scenario: force-timeout mode is disabled.
    /// Expected: the request includes forceTimeout=false.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenForceTimeoutIsFalse_AppendsFalseQueryValue()
    {
        Uri? capturedRequestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        await sut.VerifyAsync("partner-123", CancellationToken.None, false);

        capturedRequestUri.Should().NotBeNull();
        capturedRequestUri!.Query.Should().Contain("forceTimeout=false");
    }

    /// <summary>
    /// Scenario: the partner ID contains a reserved URL character.
    /// Expected: the request path escapes the partner ID safely.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenPartnerIdContainsReservedCharacters_EscapesRequestPath()
    {
        Uri? capturedRequestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        await sut.VerifyAsync("partner/123", CancellationToken.None);

        capturedRequestUri.Should().NotBeNull();
        capturedRequestUri!.AbsolutePath.Should().Contain("partner%2F123");
    }

    /// <summary>
    /// Scenario: external cancellation is requested while verifying.
    /// Expected: task cancellation propagates to the caller.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenCancellationRequested_PropagatesTaskCanceledException()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await sut.VerifyAsync("partner-123", cts.Token);

        await action.Should().ThrowAsync<TaskCanceledException>();
    }

    /// <summary>
    /// Scenario: the HTTP request times out without external cancellation.
    /// Expected: verification throws an upstream-timeout exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenRequestTimesOutWithoutExternalCancellation_ThrowsUpstreamTimeoutException()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("request timed out"));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

        var action = async () => await sut.VerifyAsync("partner-timeout", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamTimeoutException>();
    }

    /// <summary>
    /// Scenario: the HTTP client throws an HTTP request exception.
    /// Expected: verification throws an upstream-service-unavailable exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenHttpRequestExceptionThrown_ThrowsUpstreamServiceUnavailableException()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection failed"));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002/") };
        var sut = new PartnerVerifierClient(httpClient, NullLogger<PartnerVerifierClient>.Instance);

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
