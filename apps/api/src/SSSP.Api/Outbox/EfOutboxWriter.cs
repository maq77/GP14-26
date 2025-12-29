using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Outbox;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.Api.Outbox;

public sealed class EfOutboxWriter : IOutboxWriter
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EfOutboxWriter> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public EfOutboxWriter(IUnitOfWork uow, ILogger<EfOutboxWriter> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task EnqueueAsync<T>(
        string aggregateType,
        string aggregateId,
        string topic,
        string @event,
        string scope,
        string? scopeKey,
        T payload,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var repo = _uow.GetRepository<OutboxMessage, long>();

        var msg = new OutboxMessage
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Topic = topic,
            Event = @event,
            Scope = scope,
            ScopeKey = scopeKey,
            IdempotencyKey = idempotencyKey,
            PayloadType = typeof(T).FullName ?? typeof(T).Name,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOpts),
            OccurredAtUtc = DateTime.UtcNow,
            Status = 0,
            Attempts = 0
        };

        await repo.AddAsync(msg, ct);

        _logger.LogDebug("Outbox enqueued. Topic={Topic}, Event={Event}, Scope={Scope}, ScopeKey={ScopeKey}",
            topic, @event, scope, scopeKey);
    }
}
