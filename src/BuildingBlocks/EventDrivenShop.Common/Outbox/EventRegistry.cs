using System.Collections.Concurrent;
using System.Text.Json;
using EventDrivenShop.Contracts;

namespace EventDrivenShop.Common.Outbox;

/// <summary>
/// Safe contract registry mapping event type names to explicitly allowed integration event types.
/// Prevents unsafe arbitrary type instantiation from unvalidated payloads.
/// </summary>
public static class EventRegistry
{
    private static readonly ConcurrentDictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);

    static EventRegistry()
    {
        Register<OrderCreatedIntegrationEvent>();
        Register<PaymentCompletedIntegrationEvent>();
        Register<PaymentFailedIntegrationEvent>();
    }

    public static void Register<TEvent>() where TEvent : class, IIntegrationEvent
    {
        var type = typeof(TEvent);
        if (type.FullName != null) _registry[type.FullName] = type;
        _registry[type.Name] = type;
    }

    public static bool TryGetType(string eventTypeName, out Type? eventType)
    {
        return _registry.TryGetValue(eventTypeName, out eventType);
    }

    public static object? Deserialize(string eventTypeName, string jsonPayload)
    {
        if (!TryGetType(eventTypeName, out var targetType) || targetType is null)
        {
            throw new InvalidOperationException($"Unrecognized or unregistered integration event type: '{eventTypeName}'");
        }

        return JsonSerializer.Deserialize(jsonPayload, targetType, OutboxJsonOptions.Default);
    }
}
