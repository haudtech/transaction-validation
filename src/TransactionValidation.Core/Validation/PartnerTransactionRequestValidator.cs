using FluentValidation;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Validation;

public sealed class PartnerTransactionRequestValidator : AbstractValidator<PartnerTransactionRequest>
{
    private static readonly string[] ValidCurrencies = ["USD", "EUR", "GBP", "JPY", "CAD", "AUD"];

    public PartnerTransactionRequestValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty()
            .WithMessage("partnerId is required.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty()
            .WithMessage("transactionReference is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("currency is required.")
            .Must(currency => ValidCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
            .WithMessage("currency must be a supported ISO code.");

        RuleFor(x => x.Timestamp)
            .NotEqual(default(DateTime))
            .WithMessage("timestamp is required.");
    }
}
