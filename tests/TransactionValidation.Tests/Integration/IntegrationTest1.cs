using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Mock.Controllers;
using Xunit;

namespace TransactionValidation.Tests.Integration;

public class MockPartnerVerificationControllerTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public void VerifyPartner_WhenForcedTimeout_Returns408WithTimeoutPayload()
    {
        var controller = new MockPartnerVerificationController();

        var result = controller.VerifyPartner("partner-timeout", true);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status408RequestTimeout);

        var payload = objectResult.Value.Should().BeAssignableTo<object>().Subject;
        payload.ToString().Should().Contain("Simulated timeout");
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void VerifyPartner_WhenForcedSuccess_Returns200VerifiedTrue()
    {
        var controller = new MockPartnerVerificationController();

        var result = controller.VerifyPartner("partner-ok", false);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var payload = okResult.Value.Should().BeAssignableTo<object>().Subject;
        payload.ToString().Should().Contain("verified = True");
    }
}
