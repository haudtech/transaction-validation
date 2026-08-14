#nullable enable

using System.Net;
using System.Net.Http;
using FluentAssertions;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Integration;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Integration;

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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
