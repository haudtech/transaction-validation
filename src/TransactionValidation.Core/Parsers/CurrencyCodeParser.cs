using TransactionValidation.Core.Enums;

namespace TransactionValidation.Core.Parsers;

/// <summary>
/// Parses external currency-code text into a system-supported currency code.
/// </summary>
public static class CurrencyCodeParser
{
    /// <summary>
    /// Determines whether the supplied text names a currency supported by the system.
    /// </summary>
    /// <param name="value">The external currency-code value.</param>
    /// <returns>True when the value names a supported currency; otherwise false.</returns>
    public static bool IsSupported(string? value)
    {
        return TryParse(value, out _);
    }

    /// <summary>
    /// Parses a supported currency code, ignoring surrounding whitespace and letter casing.
    /// </summary>
    /// <param name="value">The external currency-code value.</param>
    /// <param name="currencyCode">The parsed currency code, or the default enum value when parsing fails.</param>
    /// <returns>True when the value names a supported currency; otherwise false.</returns>
    public static bool TryParse(string? value, out CurrencyCode currencyCode)
    {
        currencyCode = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Length == 3
            && normalized.All(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            && Enum.TryParse(normalized, ignoreCase: true, out currencyCode)
            && Enum.IsDefined(currencyCode);
    }
}
