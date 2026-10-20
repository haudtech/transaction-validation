using FluentValidation;

using TransactionValidation.Core.Models;
using TransactionValidation.Core.Parsers;

namespace TransactionValidation.Core.Validation;

/// <summary>
/// FluentValidation rules for partner-transaction requests.
/// </summary>
public sealed class PartnerTransactionRequestValidator : AbstractValidator<PartnerTransactionRequest>
{
    /// <summary>
    /// Initializes the validation rules for partner transaction payloads.
    /// </summary>
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
            .Must(CurrencyCodeParser.IsSupported)
            .WithMessage("currency must be a supported currency code (USD or EUR).");

        RuleFor(x => x.Timestamp)
            .NotEqual(default(DateTime))
            .WithMessage("timestamp is required.");
    }
}
