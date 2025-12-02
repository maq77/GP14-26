using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

namespace SSSP.Api.DTOs.Incidents
{
    public sealed class CreateIncidentRequest
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public IncidentType Type { get; set; } = IncidentType.Other;

        public IncidentSource Source { get; set; } = IncidentSource.Manual;

        public int? OperatorId { get; set; }

        public Location? Location { get; set; } = new Location();

        public string? PayloadJson { get; set; }
    }
}
