using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Integration;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Configuration;

/// <summary>
/// Verifies that PartnerVerifierClient uses the configured resilience pipeline
/// to retry transient upstream failures and fail after retry exhaustion.
/// </summary>
public sealed class PartnerVerifierResilienceRetryTests
{
    /// <summary>
    /// Ensures transient 503 responses are retried and a later success is returned.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenTransient503ThenSuccess_RetriesAndReturnsTrue()
    {
        var attempts = 0;
        var handler = new CountingHandler(() =>
        {
            attempts++;
            return attempts < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var provider = BuildProvider(handler, maxRetryAttempts: 2);
        var verifier = provider.GetRequiredService<IPartnerVerifier>();

        var result = await verifier.VerifyAsync("partner-123", CancellationToken.None);

        result.Should().BeTrue();
        attempts.Should().Be(3);
    }

    /// <summary>
    /// Ensures persistent 503 responses consume the retry budget and surface
    /// a service-unavailable exception.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenTransient503Persists_RetriesThenThrowsServiceUnavailable()
    {
        var attempts = 0;
        var handler = new CountingHandler(() =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        using var provider = BuildProvider(handler, maxRetryAttempts: 1);
        var verifier = provider.GetRequiredService<IPartnerVerifier>();

        var action = async () => await verifier.VerifyAsync("partner-123", CancellationToken.None);

        await action.Should().ThrowAsync<UpstreamServiceUnavailableException>();
        attempts.Should().Be(2);
    }

    /// <summary>
    /// Builds a minimal DI container with PartnerVerifierClient wired through
    /// AddStandardResilienceHandler and a test HTTP message handler.
    /// </summary>
    private static ServiceProvider BuildProvider(HttpMessageHandler primaryHandler, int maxRetryAttempts)
    {
        var services = new ServiceCollection();

        services.AddHttpClient<IPartnerVerifier, PartnerVerifierClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5002/");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = maxRetryAttempts;
            options.Retry.Delay = TimeSpan.FromMilliseconds(1);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);

            options.CircuitBreaker.MinimumThroughput = 100;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(1);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Provides deterministic HTTP responses and allows callers to count
    /// actual send attempts made through the resilience pipeline.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        /// <summary>
        /// Creates a counting handler with a response factory invoked per request.
        /// </summary>
        public CountingHandler(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        /// <summary>
        /// Returns the next response produced by the configured factory.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory());
        }
    }
}
