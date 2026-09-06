namespace EventDrivenShop.Observability;

/// <summary>
/// Ambient context for tracking CorrelationId across async execution scopes and threads.
/// </summary>
public static class CorrelationIdContext
{
    private static readonly AsyncLocal<string?> _currentCorrelationId = new();

    public static string CorrelationId
    {
        get => _currentCorrelationId.Value ?? string.Empty;
        set => _currentCorrelationId.Value = value;
    }

    public static IDisposable Set(string correlationId)
    {
        var prior = _currentCorrelationId.Value;
        _currentCorrelationId.Value = correlationId;
        return new DisposableScope(() => _currentCorrelationId.Value = prior);
    }

    private sealed class DisposableScope(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
