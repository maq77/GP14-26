using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSSP.BL.Outbox;

public interface IOutboxWriter
{
    Task EnqueueAsync<T>(
        string aggregateType,
        string aggregateId,
        string topic,
        string @event,
        string scope,
        string? scopeKey,
        T payload,
        string? idempotencyKey,
        CancellationToken ct);
}
