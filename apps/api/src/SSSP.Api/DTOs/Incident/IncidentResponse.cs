using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

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
}
