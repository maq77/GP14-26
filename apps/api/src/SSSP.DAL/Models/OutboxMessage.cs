using System;
using System.ComponentModel.DataAnnotations;
using SSSP.DAL.Abstractions;

namespace SSSP.DAL.Models;

public sealed class OutboxMessage : IEntity<long>
{
    public long Id { get; set; }

    [MaxLength(128)]
    public string AggregateType { get; set; } = default!;

    [MaxLength(64)]
    public string AggregateId { get; set; } = default!;

    [MaxLength(64)]
    public string Topic { get; set; } = default!;

    [MaxLength(64)]
    public string Event { get; set; } = default!;

    [MaxLength(32)]
    public string Scope { get; set; } = default!;

    [MaxLength(128)]
    public string? ScopeKey { get; set; }

    [MaxLength(256)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(256)]
    public string PayloadType { get; set; } = default!;

    public string PayloadJson { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 0 = Pending, 1 = Processed, 2 = Failed
    /// </summary>
    public int Status { get; set; }

    public int Attempts { get; set; }

    [MaxLength(2048)]
    public string? LastError { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;
}
