using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.DAL.Models;

namespace SSSP.BL.Outbox;

public interface IOutboxReader
{
    Task<IReadOnlyList<OutboxMessage>> DequeueBatchAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(long id, CancellationToken ct);
    Task MarkFailedAsync(long id, string error, CancellationToken ct);
}
