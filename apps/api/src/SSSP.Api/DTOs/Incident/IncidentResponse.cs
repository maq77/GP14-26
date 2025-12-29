using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

public sealed record IncidentResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    public IncidentType Type { get; init; }
    public IncidentSeverity Severity { get; init; }
    public IncidentStatus Status { get; init; }
    public IncidentSource Source { get; init; }

    public int? OperatorId { get; init; }
    public Location? Location { get; init; }

    public Guid? AssignedToUserId { get; init; }
    public DateTime Timestamp { get; init; }

    public DateTime? AssignedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}
