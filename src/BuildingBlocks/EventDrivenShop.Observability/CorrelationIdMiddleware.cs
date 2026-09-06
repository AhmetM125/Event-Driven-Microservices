using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace EventDrivenShop.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var incoming) &&
            !string.IsNullOrWhiteSpace(incoming))
        {
            correlationId = incoming.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.Items[CorrelationIdHeaderName] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (CorrelationIdContext.Set(correlationId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
