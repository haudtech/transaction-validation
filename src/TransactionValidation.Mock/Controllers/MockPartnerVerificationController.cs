using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TransactionValidation.Mock.Controllers;

/// <summary>
/// Simulates the upstream partner verification service used by the BFF during local development and testing.
/// It intentionally returns a successful response most of the time and a timeout response about 30% of the time to validate retry and circuit-breaker behavior.
/// </summary>
[ApiController]
[Route("api/v1/mock/partner-verification")]
public sealed class MockPartnerVerificationController : ControllerBase
{
    private const double TimeoutRate = 0.30;

    /// <summary>
    /// Simulates the partner verification API and randomly returns a timeout response to exercise resilience policies.
    /// </summary>
    /// <param name="partnerId">The partner identifier being verified.</param>
    /// <param name="forceTimeout">Optional test override that forces the timeout path.</param>
    /// <returns>HTTP 200 with a verified response or HTTP 408 when the timeout simulation triggers.</returns>
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
