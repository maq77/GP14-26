using SSSP.DAL.Models;
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
        CancellationToken ct);

    Task AssignAsync(int incidentId, Guid userId, CancellationToken ct);
    Task ResolveAsync(int incidentId, CancellationToken ct);
    Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken ct);
    Task<IReadOnlyList<Incident>> GetByOperatorAsync(int operatorId, CancellationToken ct);
}
