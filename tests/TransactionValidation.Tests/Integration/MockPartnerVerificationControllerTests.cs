using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using TransactionValidation.Mock.Controllers;

using Xunit;

namespace TransactionValidation.Tests.Integration;

/// <summary>
/// Validates deterministic and statistical behaviors of the mock partner verification controller.
/// </summary>
public class MockPartnerVerificationControllerTests
{
    [Trait("Category", "Integration")]
    [Trait("Feature", "MockVerification")]
    [Fact(DisplayName = "Mock VerifyPartner returns 408 with timeout payload when forceTimeout=true")]
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
    [Trait("Feature", "MockVerification")]
    [Fact(DisplayName = "Mock VerifyPartner returns 200 with verified=true when forceTimeout=false")]
    public void VerifyPartner_WhenForcedSuccess_Returns200VerifiedTrue()
    {
        var controller = new MockPartnerVerificationController();

        var result = controller.VerifyPartner("partner-ok", false);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var payload = okResult.Value.Should().BeAssignableTo<object>().Subject;
        payload.ToString().Should().Contain("verified = True");
    }

    [Trait("Category", "Integration")]
    [Trait("Feature", "MockVerification")]
    [Fact(DisplayName = "Mock VerifyPartner random path produces timeout rate near 30%")]
    public void VerifyPartner_WhenForceTimeoutNotProvided_TimeoutRateIsApproximatelyThirtyPercent()
    {
        var controller = new MockPartnerVerificationController();
        const int sampleSize = 2000;
        var timeoutCount = 0;

        for (var i = 0; i < sampleSize; i++)
        {
            var result = controller.VerifyPartner($"partner-{i}", null);

            if (result is ObjectResult objectResult
                && objectResult.StatusCode == StatusCodes.Status408RequestTimeout)
            {
                timeoutCount++;
            }
        }

        var timeoutRate = (double)timeoutCount / sampleSize;

        // Keep a broad statistical band to minimize random test flakiness while still validating behavior intent.
        timeoutRate.Should().BeInRange(0.22, 0.38);
    }
}
