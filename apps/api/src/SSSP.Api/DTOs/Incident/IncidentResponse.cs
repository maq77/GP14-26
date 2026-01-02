using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

<<<<<<< HEAD
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
=======
namespace SSSP.Api.DTOs.Incidents
{
    public sealed class IncidentResponse
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public IncidentType Type { get; set; } = IncidentType.Other;

        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Low;

        public IncidentStatus Status { get; set; } = IncidentStatus.Open;

        public IncidentSource Source { get; set; } = IncidentSource.Manual;

        public int? OperatorId { get; set; }

        public Location? Location { get; set; }

        public Guid? AssignedToUserId { get; set; }

        public DateTime Timestamp { get; set; }

        public DateTime? ResolvedAt { get; set; }
    }
>>>>>>> main
}
