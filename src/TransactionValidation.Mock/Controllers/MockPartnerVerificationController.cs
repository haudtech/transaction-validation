using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace TransactionValidation.Mock.Controllers;

[ApiController]
[Route("api/v1/mock/partner-verification")]
/// <summary>
/// Simulates the upstream partner verification service used by the BFF during local development and testing.
/// It intentionally returns a successful response most of the time and a timeout response about 30% of the time to validate retry and circuit-breaker behavior.
/// </summary>
public sealed class MockPartnerVerificationController : ControllerBase
{
    private const double TimeoutRate = 0.30;

    [HttpGet("verify/{partnerId}")]
    public IActionResult VerifyPartner(string partnerId, [FromQuery] bool? forceTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            return BadRequest(new { error = "partnerId is required." });
        }

        var shouldTimeout = forceTimeout ?? (Random.Shared.NextDouble() < TimeoutRate);
        if (shouldTimeout)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, new
            {
                partnerId,
                verified = false,
                reason = "Simulated timeout"
            });
        }

        return Ok(new { partnerId, verified = true });
    }
}
