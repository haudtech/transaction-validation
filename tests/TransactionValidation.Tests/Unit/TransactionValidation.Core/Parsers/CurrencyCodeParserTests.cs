using FluentAssertions;

using TransactionValidation.Core.Enums;
using TransactionValidation.Core.Parsers;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Core.Parsers;

/// <summary>
/// Verifies parsing and support checks for the system currency allowlist.
/// </summary>
public sealed class CurrencyCodeParserTests
{
    [Theory]
    [InlineData("USD", CurrencyCode.USD)]
    [InlineData("EUR", CurrencyCode.EUR)]
    [InlineData(" usd ", CurrencyCode.USD)]
    [InlineData("eur", CurrencyCode.EUR)]
    public void TryParse_WhenValueIsSupported_ReturnsNormalizedCurrencyCode(string value, CurrencyCode expected)
    {
        var parsed = CurrencyCodeParser.TryParse(value, out var currencyCode);

        parsed.Should().BeTrue();
        currencyCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("GBP")]
    [InlineData("123")]
    [InlineData("U$D")]
    public void TryParse_WhenValueIsInvalidOrUnsupported_ReturnsFalseAndDefault(string? value)
    {
        var parsed = CurrencyCodeParser.TryParse(value, out var currencyCode);

        parsed.Should().BeFalse();
        currencyCode.Should().Be(default);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("eur")]
    [InlineData(" EUR ")]
    public void IsSupported_WhenValueIsSupported_ReturnsTrue(string value)
    {
        CurrencyCodeParser.IsSupported(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GBP")]
    [InlineData("1")]
    public void IsSupported_WhenValueIsInvalidOrUnsupported_ReturnsFalse(string? value)
    {
        CurrencyCodeParser.IsSupported(value).Should().BeFalse();
    }
}
