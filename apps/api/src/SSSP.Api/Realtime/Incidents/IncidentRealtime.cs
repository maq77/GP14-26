using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Incidents;
using SSSP.Api.Realtime;
using SSSP.Api.Realtime.Contracts.Incidents;
using SSSP.BL.Realtime.Incidents;

public sealed class IncidentRealtime : IIncidentRealtime
{
    private readonly INotificationPublisher _rt;
    private readonly ILogger<IncidentRealtime> _logger;

    public IncidentRealtime(INotificationPublisher rt, ILogger<IncidentRealtime> logger)
    {
        _rt = rt;
        _logger = logger;
    }

    public async Task CreatedAsync(Incident incident, CancellationToken ct)
    {
        try
        {
            var dto = ToResponse(incident);

            if (incident.OperatorId.HasValue)
                await _rt.ToOperatorAsync(incident.OperatorId.Value, IncidentTopics.Topic, IncidentTopics.Created, dto, ct);
            else
                await _rt.ToGlobalAsync(IncidentTopics.Topic, IncidentTopics.Created, dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Realtime publish failed for IncidentCreated. Id={Id}", incident.Id);
        }
    }

    public async Task AssignedAsync(Incident incident, Guid assigneeUserId, CancellationToken ct)
    {
        try
        {
            var assignedPayload = new IncidentAssignedPayload(incident.Id, assigneeUserId);

            // incident-scoped
            await _rt.ToIncidentAsync(incident.Id, IncidentTopics.Topic, IncidentTopics.Assigned, assignedPayload, ct);

            // user-scoped
            await _rt.ToUserAsync(assigneeUserId, IncidentTopics.Topic, IncidentTopics.Assigned, assignedPayload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Realtime publish failed for IncidentAssigned. Id={Id}", incident.Id);
        }
    }

    public async Task StatusChangedAsync(Incident incident, CancellationToken ct)
    {
        try
        {
            var dto = ToResponse(incident);

            await _rt.ToIncidentAsync(incident.Id, IncidentTopics.Topic, IncidentTopics.StatusChanged, dto, ct);

            if (incident.OperatorId.HasValue)
                await _rt.ToOperatorAsync(incident.OperatorId.Value, IncidentTopics.Topic, IncidentTopics.StatusChanged, dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Realtime publish failed for StatusChanged. Id={Id}", incident.Id);
        }
    }

    public async Task ClosedAsync(Incident incident, CancellationToken ct)
    {
        try
        {
            var dto = ToResponse(incident);

            await _rt.ToIncidentAsync(incident.Id, IncidentTopics.Topic, IncidentTopics.Closed, dto, ct);

            if (incident.OperatorId.HasValue)
                await _rt.ToOperatorAsync(incident.OperatorId.Value, IncidentTopics.Topic, IncidentTopics.Closed, dto, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Realtime publish failed for Closed. Id={Id}", incident.Id);
        }
    }

    private static IncidentResponse ToResponse(Incident incident)
        => new()
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Description,
            Type = incident.Type,
            Severity = incident.Severity,
            Status = incident.Status,
            Source = incident.Source,
            OperatorId = incident.OperatorId,
            Location = incident.Location,
            AssignedToUserId = incident.AssignedToUserId,
            Timestamp = incident.Timestamp,
            AssignedAt = incident.AssignedAt,
            StartedAt = incident.StartedAt,
            ResolvedAt = incident.ResolvedAt,
            ClosedAt = incident.ClosedAt
        };
}
