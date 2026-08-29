using System.Net;

using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Integration;

/// <summary>
/// Calls the mock partner verification endpoint and translates HTTP outcomes into the BFF's domain exceptions.
/// It provides the upstream verification step required by the architecture and resilience recommendations.
/// </summary>
public sealed class PartnerVerifierClient : IPartnerVerifier
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes the client with the configured HTTP client for the mocked verification service.
    /// </summary>
    /// <param name="httpClient">The outbound HTTP client used to reach the partner verification endpoint.</param>
    public PartnerVerifierClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Calls the partner verification endpoint and maps HTTP and timeout outcomes to the correct domain exceptions.
    /// </summary>
    /// <param name="partnerId">The partner identifier being verified.</param>
    /// <param name="cancellationToken">Token that can cancel the outbound request.</param>
    /// <param name="forceTimeout">Optional override used during tests to force the timeout path.</param>
    /// <returns>True when the partner verification endpoint accepts the request; otherwise throws a domain exception.</returns>
    public async Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default, bool? forceTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            throw new BadRequestException("partnerId is required.");
        }

        var requestPath = $"api/v1/mock/partner-verification/verify/{Uri.EscapeDataString(partnerId)}";
        if (forceTimeout.HasValue)
        {
            requestPath += $"?forceTimeout={forceTimeout.Value.ToString().ToLowerInvariant()}";
        }

        try
        {
            var response = await _httpClient.GetAsync(requestPath, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException($"Partner '{partnerId}' could not be verified.");
            }

            if (response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new UpstreamTimeoutException("Partner verification request timed out.");
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable
                || response.StatusCode >= HttpStatusCode.InternalServerError)
            {
                throw new UpstreamServiceUnavailableException("Partner verification service is unavailable.");
            }

            throw new UpstreamServiceUnavailableException(
                $"Partner verification returned unexpected status code {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamTimeoutException("Partner verification request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new UpstreamServiceUnavailableException("Partner verification service is unavailable.");
        }
    }
}
