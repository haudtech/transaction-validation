using System.Globalization;
using FluentValidation;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Validation;

/// <summary>
/// FluentValidation rules for partner-transaction requests.
/// These checks enforce the required fields and ISO currency/amount validation described in docs/analysis/solution_analysis.md.
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
            .Must(IsIso4217CurrencyCode)
            .WithMessage("currency must be a valid ISO-4217 code.");

        RuleFor(x => x.Timestamp)
            .NotEqual(default(DateTime))
            .WithMessage("timestamp is required.");
    }

    /// <summary>
    /// Validates whether the provided currency text resolves to a known ISO-4217
    /// currency symbol after trimming and uppercasing.
    /// </summary>
    /// <remarks>
    /// Reference sources:
    /// - ISO 4217 standard overview: https://www.iso.org/iso-4217-currency-codes.html
    /// - SIX Group official ISO currency list (ISO 4217 maintenance agency publication):
    ///   https://www.six-group.com/en/products-services/financial-information/data-standards.html
    /// - Codes for representation of currencies and funds:
    ///   https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml
    /// </remarks>
    /// <summary>
    /// Checks whether the supplied currency matches a known ISO-4217 code after trimming and uppercasing.
    /// </summary>
    /// <param name="currency">The candidate currency code.</param>
    /// <returns>True when the value is a valid ISO-4217 code; otherwise false.</returns>
    private static bool IsIso4217CurrencyCode(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return false;
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return false;
        }

        // Build the ISO currency set from available specific cultures.
        var isoCodes = CultureInfo
            .GetCultures(CultureTypes.SpecificCultures)
            .Select(culture =>
            {
                try
                {
                    return new RegionInfo(culture.Name).ISOCurrencySymbol;
                }
                catch
                {
                    return null;
                }
            })
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return isoCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
