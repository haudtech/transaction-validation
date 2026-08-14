using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;

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

        var response = await _httpClient.GetAsync(requestPath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new NotFoundException($"Partner '{partnerId}' could not be verified.");
        }

        return true;
    }
}
