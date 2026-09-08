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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenPartnerIdIsMissing_ReturnsPartnerIdError(string? partnerId)
    {
        var request = CreateValidRequest(partnerId: partnerId);

        var result = validator.Validate(request);

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(PartnerTransactionRequest.PartnerId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenAmountIsNotPositive_ReturnsAmountError(decimal amount)
    {
        var request = CreateValidRequest(amount: amount);

        var result = validator.Validate(request);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(PartnerTransactionRequest.Amount)
            && error.ErrorMessage == "amount must be greater than zero.");
    }

    [Fact]
    public void Validate_WhenTransactionReferenceIsMissing_ReturnsReferenceError()
    {
        var request = CreateValidRequest(transactionReference: " ");

        var result = validator.Validate(request);

        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(PartnerTransactionRequest.TransactionReference));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    public void Validate_WhenCurrencyIsMissingOrMalformed_ReturnsCurrencyError(string? currency)
    {
        var request = CreateValidRequest(currency: currency);

        var result = validator.Validate(request);

        result.Errors.Should().Contain(error => error.PropertyName == nameof(PartnerTransactionRequest.Currency));
    }

    private static PartnerTransactionRequest CreateValidRequest(
        string? partnerId = "partner-123",
        string? transactionReference = "txn-001",
        decimal amount = 250.00m,
        string? currency = "EUR",
        DateTime? timestamp = null)
    {
        return new PartnerTransactionRequest
        {
            PartnerId = partnerId!,
            TransactionReference = transactionReference!,
            Amount = amount,
            Currency = currency!,
            Timestamp = timestamp ?? DateTime.UtcNow
        };
    }
}
