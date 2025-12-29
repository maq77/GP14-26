using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSSP.Api.Realtime.Contracts;

/// <summary>
/// Raw envelope (Data is JsonElement) - safest for generic routing.
/// </summary>
public sealed record RealtimeEnvelope(
    string Topic,
    string Event,
    JsonElement Data,
    DateTimeOffset TsUtc,
    string? CorrelationId = null
);

/// <summary>
/// Typed envelope for strongly-typed client methods.
/// NOTE: Method must be non-generic, but parameter types can be generic instantiations.
/// </summary>
public sealed record RealtimeEnvelope<T>(
    string Topic,
    string Event,
    T Data,
    DateTimeOffset TsUtc,
    string? CorrelationId = null
);
