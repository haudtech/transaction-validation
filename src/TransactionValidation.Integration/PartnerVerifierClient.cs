using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;
using System.Net;

namespace TransactionValidation.Integration;

public sealed class PartnerVerifierClient : IPartnerVerifier
{
    private readonly HttpClient _httpClient;

    public PartnerVerifierClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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
