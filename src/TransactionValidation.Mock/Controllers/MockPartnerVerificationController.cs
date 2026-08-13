using Microsoft.AspNetCore.Mvc;

namespace TransactionValidation.Mock.Controllers;

[ApiController]
[Route("api/v1/mock/partner-verification")]
public sealed class MockPartnerVerificationController : ControllerBase
{
    private static readonly Random Random = new();

    [HttpGet("verify/{partnerId}")]
    public IActionResult VerifyPartner(string partnerId)
    {
        // Simple deterministic placeholder: always return verified = true
        return Ok(new { partnerId, verified = true });
    }
}
