namespace EventDrivenShop.PaymentService.Application.Services;

public sealed record PaymentSimulationResult(bool IsSuccess, string? FailureReason);

public interface IPaymentSimulator
{
    PaymentSimulationResult Simulate(string customerEmail, decimal amount, string currency);
}

/// <summary>
/// Deterministic payment gateway simulation.
/// Uses customer email prefix (e.g. 'fail-payment@example.com' or 'fail@...') or currency 'FAIL' to trigger failure.
/// Ensures tests and demonstrations are 100% deterministic and non-flaky.
/// </summary>
public sealed class PaymentSimulator : IPaymentSimulator
{
    public PaymentSimulationResult Simulate(string customerEmail, decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return new PaymentSimulationResult(false, "Customer email is required for payment authorization.");
        }

        var normalizedEmail = customerEmail.Trim().ToLowerInvariant();
        if (normalizedEmail.StartsWith("fail") ||
            normalizedEmail.Contains("fail-payment") ||
            string.Equals(currency, "FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentSimulationResult(false, "Payment authorization declined: Insufficient funds / card declined.");
        }

        return new PaymentSimulationResult(true, null);
    }
}
