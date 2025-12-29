using SSSP.Api.DTOs.Incidents;

namespace SSSP.Api.Realtime.Contracts.Incidents;

public static class IncidentTopics
{
    public const string Topic = "incident";
    public const string Created = "created";
    public const string Assigned = "assigned";
    public const string StatusChanged = "status_changed";
    public const string Closed = "closed";
}

public sealed record IncidentAssignedPayload(int IncidentId, Guid UserId);
