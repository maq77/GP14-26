using System.ComponentModel.DataAnnotations;
using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

namespace SSSP.Api.DTOs.Incidents;

public sealed record CreateIncidentRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Required]
    public IncidentType Type { get; init; } = IncidentType.Other;

    [Required]
    public IncidentSource Source { get; init; } = IncidentSource.Manual;

    public int? OperatorId { get; init; }
    public Location? Location { get; init; }

    [MaxLength(100_000)]
    public string? PayloadJson { get; init; }
}
