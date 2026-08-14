using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace TransactionValidation.Mock.Controllers;

[ApiController]
[Route("api/v1/mock/partner-verification")]
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
