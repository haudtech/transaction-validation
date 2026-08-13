using FluentAssertions;
using Xunit;

using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;

namespace TransactionValidation.Core.Validation.Tests;

public class PartnerTransactionValidatorTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var request = CreateValidRequest();

        var errors = PartnerTransactionValidator.Validate(request);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenRequestHasMultipleIssues_ReturnsFieldErrors()
    {
        var request = new PartnerTransactionRequest
        {
            PartnerId = string.Empty,
            TransactionReference = string.Empty,
            Amount = 0,
            Currency = "ZZZ",
            Timestamp = default
        };

        var errors = PartnerTransactionValidator.Validate(request);

        errors.Should().HaveCount(5);
        errors.Select(e => e.Field).Should().Contain(
        [
            nameof(request.PartnerId),
            nameof(request.TransactionReference),
            nameof(request.Amount),
            nameof(request.Currency),
            nameof(request.Timestamp)
        ]);
    }

    private static PartnerTransactionRequest CreateValidRequest()
    {
        return new PartnerTransactionRequest
        {
            PartnerId = "partner-123",
            TransactionReference = "txn-001",
            Amount = 100.25m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
    }
}
