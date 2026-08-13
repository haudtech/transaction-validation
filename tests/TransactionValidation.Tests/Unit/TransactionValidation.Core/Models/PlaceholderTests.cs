using FluentAssertions;
using TransactionValidation.Core.Models;
using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Core.Models;

public class PlaceholderTests
{
    [Fact]
    public void Message_ReturnsExpected()
    {
        var sut = new Placeholder();

        var result = sut.Message;

        result.Should().Be("Core placeholder");
    }
}
