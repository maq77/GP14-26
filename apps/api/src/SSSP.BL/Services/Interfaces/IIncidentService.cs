using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

public interface IIncidentService
{
    Task<Incident> CreateAsync(
        string title,
        string description,
        IncidentType type,
        IncidentSource source,
        int? operatorId,
        Location? location,
        string? payloadJson,
        string? idempotencyKey,
        CancellationToken ct);

    Task AssignAsync(int incidentId, Guid userId, CancellationToken ct);
    Task StartWorkAsync(int incidentId, Guid actorUserId, CancellationToken ct);
    Task ResolveAsync(int incidentId, Guid actorUserId, CancellationToken ct);
    Task CloseAsync(int incidentId, CancellationToken ct);

    Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken ct);
    Task<IReadOnlyList<Incident>> GetByOperatorAsync(int operatorId, CancellationToken ct);
}
