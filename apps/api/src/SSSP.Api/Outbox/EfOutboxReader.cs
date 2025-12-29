using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SSSP.BL.Outbox;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.Api.Outbox;

public sealed class EfOutboxReader : IOutboxReader
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EfOutboxReader> _logger;

    public EfOutboxReader(IUnitOfWork uow, ILogger<EfOutboxReader> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int batchSize, CancellationToken ct)
    {
        var repo = _uow.GetRepository<OutboxMessage, long>();

        return await repo.Query
            .AsNoTracking()
            .Where(x => x.Status == 0)              // Pending
            .OrderBy(x => x.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkProcessedAsync(long id, CancellationToken ct)
    {
        var repo = _uow.GetRepository<OutboxMessage, long>();
        var msg = await repo.GetByIdAsync(id, ct);
        if (msg == null) return;

        msg.Status = 1;
        msg.ProcessedAtUtc = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct)
    {
        var repo = _uow.GetRepository<OutboxMessage, long>();
        var msg = await repo.GetByIdAsync(id, ct);
        if (msg == null) return;

        msg.Status = 2;
        msg.Attempts += 1;
        msg.LastError = error.Length > 2048 ? error[..2048] : error;

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning("Outbox failed. Id={Id}, Attempts={Attempts}", id, msg.Attempts);
    }
}
