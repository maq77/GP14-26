using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.DAL.ValueObjects;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class IncidentService : IIncidentService
    {
        private readonly IUnitOfWork _uow;
        private readonly IIncidentManager _manager;
        private readonly ILogger<IncidentService> _logger;
        private readonly TelemetryClient _telemetry;

        public IncidentService(
            IUnitOfWork uow,
            IIncidentManager manager,
            ILogger<IncidentService> logger,
            TelemetryClient telemetry)
        {
            _uow = uow;
            _manager = manager;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task<Incident> CreateAsync(
            string title,
            string description,
            IncidentType type,
            IncidentSource source,
            int? operatorId,
            Location? location,
            string? payloadJson,
            CancellationToken ct)
        {
            var severity = _manager.ResolveSeverity(type);
            var status = _manager.InitialStatus(source);

            var incident = new Incident
            {
                Title = title,
                Description = description,
                Type = type,
                Source = source,
                Severity = severity,
                Status = status,
                OperatorId = operatorId,
                Location = location,
                PayloadJson = payloadJson,
                Timestamp = DateTime.UtcNow
            };

            incident.DedupeKey = _manager.BuildDedupeKey(incident);

            var repo = _uow.GetRepository<Incident, int>();

            // DEDUPE
            var exists = await repo.Query.AnyAsync(
                i => i.DedupeKey == incident.DedupeKey &&
                     i.Status != IncidentStatus.Closed,
                ct);

            if (exists)
            {
                _logger.LogWarning("Duplicate incident blocked. DedupeKey={Key}", incident.DedupeKey);
                throw new InvalidOperationException("Duplicate incident detected.");
            }

            await repo.AddAsync(incident, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "INCIDENT CREATED. Id={Id}, Type={Type}, Severity={Severity}, Operator={OperatorId}",
                incident.Id, type, severity, operatorId);

            _telemetry.TrackEvent("IncidentCreated", new Dictionary<string, string>
            {
                ["Type"] = type.ToString(),
                ["Severity"] = severity.ToString(),
                ["Source"] = source.ToString(),
                ["Operator"] = operatorId.ToString()
            });

            _telemetry.TrackMetric("IncidentCreatedCount", 1,
                new Dictionary<string, string>
                {
                    ["Severity"] = severity.ToString(),
                    ["Source"] = source.ToString()
                });

            return incident;
        }

        public async Task AssignAsync(int incidentId, Guid userId, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Incident, int>();
            var incident = await repo.GetByIdAsync(incidentId, ct)
                ?? throw new KeyNotFoundException("Incident not found");

            incident.AssignedToUserId = userId;
            incident.Status = IncidentStatus.Assigned;

            await _uow.SaveChangesAsync(ct);

            _telemetry.TrackEvent("IncidentAssigned", new Dictionary<string, string>
            {
                ["IncidentId"] = incidentId.ToString(),
                ["UserId"] = userId.ToString()
            });
        }

        public async Task ResolveAsync(int incidentId, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Incident, int>();
            var incident = await repo.GetByIdAsync(incidentId, ct)
                ?? throw new KeyNotFoundException("Incident not found");

            incident.Status = IncidentStatus.Resolved;
            incident.ResolvedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync(ct);

            var resolutionSeconds = incident.ResolvedAt.Value.Subtract(incident.Timestamp).TotalSeconds;

            _telemetry.TrackEvent("IncidentResolved", new Dictionary<string, string>
            {
                ["IncidentId"] = incidentId.ToString()
            });

            _telemetry.TrackMetric("IncidentResolutionSeconds", resolutionSeconds,
                new Dictionary<string, string>
                {
                    ["Severity"] = incident.Severity.ToString()
                });
        }

        public async Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken ct)
        {
            var repo = _uow.GetRepository<Incident, int>();

            return await repo.Query
                .Where(x => x.Status != IncidentStatus.Closed)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Incident>> GetByOperatorAsync(int operatorId, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Incident, int>();

            return await repo.Query
                .Where(x => x.OperatorId == operatorId)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync(ct);
        }
    }
}
