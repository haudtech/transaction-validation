#nullable enable

using TransactionValidation.Core.Models;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Api.Support;

internal static class RequestFactory
{
    public static PartnerTransactionRequest CreateValidRequest(string reference) => new()
    {
        PartnerId = "partner-1",
        TransactionReference = reference,
        Amount = 100.50m,
        Currency = "USD",
        Timestamp = DateTime.UtcNow
    };
}
