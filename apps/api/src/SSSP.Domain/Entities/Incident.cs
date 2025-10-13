using SSSP.Domain.Enums;
using SSSP.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public IncidentSource Source { get; set; }
    public Guid OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public Location Location { get; set; } = new();
    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? PayloadJson { get; set; }
    public string? DedupeKey { get; set; }
}
