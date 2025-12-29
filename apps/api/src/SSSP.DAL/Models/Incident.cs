using SSSP.DAL.Abstractions;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.DAL.ValueObjects;
using System.ComponentModel.DataAnnotations;

public class Incident : IEntity<int>
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = default!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public IncidentType Type { get; set; }
    public IncidentSource Source { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; private set; } = IncidentStatus.Open;

    public int? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public Location? Location { get; set; }
    public string? PayloadJson { get; set; }

    public Guid? AssignedToUserId { get; private set; }
    public User? AssignedToUser { get; set; }

    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    [MaxLength(128)]
    public string DedupeKey { get; private set; } = default!;

    [MaxLength(128)]
    public string? IdempotencyKey { get; private set; }

    [Timestamp] // EF Core concurrency
    public byte[] RowVersion { get; set; } = default!;

    // ===== Domain Methods (State Machine) =====

    public void Initialize(
        IncidentSeverity severity,
        DateTime nowUtc,
        string dedupeKey,
        string? idempotencyKey)
    {
        Severity = severity;
        Status = IncidentStatus.Open;
        Timestamp = nowUtc;
        DedupeKey = dedupeKey;
        IdempotencyKey = idempotencyKey;
    }

    public void Assign(Guid userId, DateTime nowUtc)
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot assign when status is {Status}.");

        AssignedToUserId = userId;
        AssignedAt = nowUtc;
        Status = IncidentStatus.Assigned;
    }

    public void StartWork(Guid actorUserId, DateTime nowUtc)
    {
        if (Status != IncidentStatus.Assigned)
            throw new InvalidOperationException($"Cannot start work when status is {Status}.");

        if (AssignedToUserId != actorUserId)
            throw new UnauthorizedAccessException("Only assignee can start work.");

        StartedAt = nowUtc;
        Status = IncidentStatus.InProgress;
    }

    public void Resolve(Guid actorUserId, DateTime nowUtc)
    {
        if (Status != IncidentStatus.InProgress)
            throw new InvalidOperationException($"Cannot resolve when status is {Status}.");

        if (AssignedToUserId != actorUserId)
            throw new UnauthorizedAccessException("Only assignee can resolve.");

        ResolvedAt = nowUtc;
        Status = IncidentStatus.Resolved;
    }

    public void Close(DateTime nowUtc)
    {
        if (Status != IncidentStatus.Resolved)
            throw new InvalidOperationException($"Cannot close when status is {Status}.");

        ClosedAt = nowUtc;
        Status = IncidentStatus.Closed;
    }

    public void Reopen(string reason, DateTime nowUtc)
    {
        if (Status != IncidentStatus.Resolved && Status != IncidentStatus.Closed)
            throw new InvalidOperationException($"Cannot reopen when status is {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reopen reason is required.", nameof(reason));

        // clear assignment
        AssignedToUserId = null;
        AssignedAt = null;
        StartedAt = null;
        ResolvedAt = null;
        ClosedAt = null;

        Status = IncidentStatus.Open;
        // you can store reason in a separate IncidentHistory table later
    }
}
