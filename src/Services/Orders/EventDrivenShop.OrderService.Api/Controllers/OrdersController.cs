using EventDrivenShop.Common.Pagination;
using EventDrivenShop.Observability;
using EventDrivenShop.OrderService.Application.DTOs;
using EventDrivenShop.OrderService.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventDrivenShop.OrderService.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderApplicationService _orderService;

    public OrdersController(IOrderApplicationService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Creates a new order asynchronously.
    /// The order is atomically saved with an Outbox event and initially set to 'PendingPayment'.
    /// Payment processing occurs asynchronously via messaging.
    /// </summary>
    /// <param name="request">Order creation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created order with initial PendingPayment status.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdContext.CorrelationId;
        var order = await _orderService.CreateOrderAsync(request, correlationId, cancellationToken);

        return CreatedAtAction(
            nameof(GetOrderById),
            new { id = order.Id },
            order);
    }

    /// <summary>
    /// Retrieves a specific order by its unique ID.
    /// </summary>
    /// <param name="id">The order ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order Not Found",
                Detail = $"Order with ID '{id}' was not found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(order);
    }

    /// <summary>
    /// Lists orders with server-side pagination, status filtering, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? sortBy = "CreatedAtUtc",
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(pageNumber, pageSize, status, customerId, sortBy, sortDescending);
        var result = await _orderService.GetOrdersAsync(query, cancellationToken);
        return Ok(result);
    }
}
