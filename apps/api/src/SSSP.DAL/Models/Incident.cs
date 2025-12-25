using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;
using SSSP.DAL.Abstractions;

namespace SSSP.DAL.Models
{
    public sealed class Incident : IEntity<int>
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public IncidentType Type { get; set; } = IncidentType.Other;

        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Low;

        public IncidentStatus Status { get; set; } = IncidentStatus.Open;

        public IncidentSource Source { get; set; } = IncidentSource.Manual;

        public int? OperatorId { get; set; }
        public Operator? Operator { get; set; }

        public Location? Location { get; set; }

        public Guid? AssignedToUserId { get; set; }
        public User? AssignedToUser { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        public string? PayloadJson { get; set; }

        public string? DedupeKey { get; set; }
    }
}
