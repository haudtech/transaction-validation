using FluentAssertions;
using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;
using Xunit;

namespace TransactionValidation.Core.Validation.Tests;

/// <summary>
/// Validates request-level FluentValidation rules including ISO-4217 currency checks.
/// </summary>
public class PartnerTransactionRequestValidatorTests
{
    private readonly PartnerTransactionRequestValidator validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_IsValid()
    {
        var request = CreateValidRequest();

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenCurrencyIsUnsupported_ReturnsCurrencyError()
    {
        var request = CreateValidRequest(currency: "XYZ");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(PartnerTransactionRequest.Currency)
            && e.ErrorMessage == "currency must be a valid ISO-4217 code.");
    }

    [Fact]
    public void Validate_WhenCurrencyIsLowercaseIso_IsValid()
    {
        var request = CreateValidRequest(currency: "usd");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTimestampIsDefault_ReturnsTimestampError()
    {
        var request = CreateValidRequest(timestamp: default(DateTime));

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(PartnerTransactionRequest.Timestamp)
            && e.ErrorMessage == "timestamp is required.");
    }

    private static PartnerTransactionRequest CreateValidRequest(string currency = "EUR", DateTime? timestamp = null)
    {
        return new PartnerTransactionRequest
        {
            PartnerId = "partner-123",
            TransactionReference = "txn-001",
            Amount = 250.00m,
            Currency = currency,
            Timestamp = timestamp ?? DateTime.UtcNow
        };
    }
}
