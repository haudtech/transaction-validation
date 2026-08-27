using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Mock.Services;

namespace TransactionValidation.Mock.Controllers;

[ApiController]
[Route("api/v1/mock/consumer-observations")]
public sealed class ConsumerObservationController : ControllerBase
{
    private readonly ConsumerObservationStore _observationStore;
    private readonly ConsumerFailureControl _failureControl;

    public ConsumerObservationController(
        ConsumerObservationStore observationStore,
        ConsumerFailureControl failureControl)
    {
        _observationStore = observationStore;
        _failureControl = failureControl;
    }

    [HttpGet("{consumerName}")]
    public ActionResult<IReadOnlyCollection<ConsumerObservation>> Get(string consumerName)
    {
        return Ok(_observationStore.Get(consumerName));
    }

    [HttpPost("{consumerName}/fail-before-ack/{messageId}")]
    public IActionResult FailBeforeAcknowledgement(string consumerName, string messageId)
    {
        _failureControl.FailBeforeAcknowledgement(consumerName, messageId);
        return Accepted();
    }
}
