using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Validation;

/// <summary>
/// Manual validation helper for transaction payloads that returns field-level errors for the BFF request contract.
/// It complements the FluentValidation rules used at runtime and matches the design's validation guidance.
/// </summary>
public static class PartnerTransactionValidator
{
    private static readonly HashSet<string> ValidCurrencies =
    [
        "USD",
        "EUR",
        "GBP",
        "JPY",
        "CAD",
        "AUD"
    ];

    public static List<FieldError> Validate(PartnerTransactionRequest request)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.PartnerId))
        {
            errors.Add(new FieldError
            {
                Field = nameof(request.PartnerId),
                Message = "partnerId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TransactionReference))
        {
            errors.Add(new FieldError
            {
                Field = nameof(request.TransactionReference),
                Message = "transactionReference is required."
            });
        }

        if (request.Amount <= 0)
        {
            errors.Add(new FieldError
            {
                Field = nameof(request.Amount),
                Message = "amount must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Currency)
            || !ValidCurrencies.Contains(request.Currency, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new FieldError
            {
                Field = nameof(request.Currency),
                Message = "currency is required and must be a supported ISO code."
            });
        }

        if (request.Timestamp == default)
        {
            errors.Add(new FieldError
            {
                Field = nameof(request.Timestamp),
                Message = "timestamp is required and must be a valid UTC time."
            });
        }

        return errors;
    }
}
