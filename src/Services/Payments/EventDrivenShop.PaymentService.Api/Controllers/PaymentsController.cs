using EventDrivenShop.PaymentService.Application.DTOs;
using EventDrivenShop.PaymentService.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventDrivenShop.PaymentService.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentApplicationService _paymentService;

    public PaymentsController(IPaymentApplicationService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// Retrieves payment record by payment ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(id, cancellationToken);
        if (payment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment Not Found",
                Detail = $"Payment with ID '{id}' was not found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(payment);
    }

    /// <summary>
    /// Retrieves payment record by associated order ID.
    /// </summary>
    [HttpGet("by-order/{orderId:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOrderId([FromRoute] Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetPaymentByOrderIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment Not Found",
                Detail = $"No payment record exists for Order ID '{orderId}'.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(payment);
    }
}
