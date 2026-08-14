using TransactionValidation.Core.Exceptions;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Integration;

public sealed class PartnerVerifierPlaceholder : IPartnerVerifier
{
    private readonly HttpClient _httpClient;

    public PartnerVerifierPlaceholder(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            throw new BadRequestException("partnerId is required.");
        }

        var response = await _httpClient.GetAsync($"api/v1/mock/partner-verification/verify/{partnerId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new NotFoundException($"Partner '{partnerId}' could not be verified.");
        }

        return true;
    }
}
