<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SSSP.BL.Outbox;
using SSSP.BL.Realtime.Incidents;
using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;
using SSSP.Infrastructure.Persistence.Interfaces;
using SSSP.Telemetry.Abstractions.Incidents;

public sealed class IncidentService : IIncidentService
{
    private readonly IUnitOfWork _uow;
    private readonly IIncidentManager _manager;
    private readonly IClock _clock;
    private readonly ILogger<IncidentService> _logger;
    private readonly IIncidentTelemetry _telemetry;
    private readonly IIncidentRealtime _realtime; // optional: keep for now
    private readonly IOutboxWriter _outbox;

    public IncidentService(
        IUnitOfWork uow,
        IIncidentManager manager,
        IClock clock,
        ILogger<IncidentService> logger,
        IIncidentTelemetry telemetry,
        IIncidentRealtime realtime,
        IOutboxWriter outbox)
    {
        _uow = uow;
        _manager = manager;
        _clock = clock;
        _logger = logger;
        _telemetry = telemetry;
        _realtime = realtime;
        _outbox = outbox;
    }

    public async Task<Incident> CreateAsync(
        string title,
        string description,
        IncidentType type,
        IncidentSource source,
        int? operatorId,
        Location? location,
        string? payloadJson,
        string? idempotencyKey,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var now = _clock.UtcNow;
        var repo = _uow.GetRepository<Incident, int>();

        // Idempotency fast-path
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await repo.Query.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

            if (existing != null)
                return existing;
        }

        var severity = _manager.ResolveSeverity(type);
        var dedupeKey = _manager.BuildDedupeKey(type, source, operatorId, now);

        // Best-effort (DB filtered unique index هو الضمان الحقيقي)
        var dupExists = await repo.Query.AnyAsync(
            i => i.DedupeKey == dedupeKey && i.Status != IncidentStatus.Closed,
            ct);

        if (dupExists)
            throw new InvalidOperationException("Duplicate incident detected.");

        var incident = new Incident
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Type = type,
            Source = source,
            OperatorId = operatorId,
            Location = location,
            PayloadJson = payloadJson
        };

        incident.Initialize(severity, now, dedupeKey, idempotencyKey);

        await _uow.BeginTransactionAsync(ct);

        try
        {
            await repo.AddAsync(incident, ct);

            // Save #1 => generates incident.Id
            await _uow.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                aggregateType: "incident",
                aggregateId: incident.Id.ToString(),
                topic: "incident",
                @event: "created",
                scope: incident.OperatorId.HasValue ? "operator" : "global",
                scopeKey: incident.OperatorId?.ToString(),
                payload: new
                {
                    Id = incident.Id,
                    Title = incident.Title,
                    Description = incident.Description,
                    Type = incident.Type.ToString(),
                    Status = incident.Status.ToString(),
                    Severity = incident.Severity.ToString(),
                    Source = incident.Source.ToString(),
                    OperatorId = incident.OperatorId,
                    Timestamp = incident.Timestamp
                },
                idempotencyKey: incident.IdempotencyKey is null ? null : $"incident.created:{incident.IdempotencyKey}",
                ct: ct);

            // Save #2 => persists outbox row
            await _uow.SaveChangesAsync(ct);

            await _uow.CommitTransactionAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await _uow.RollbackTransactionAsync(ct);

            _logger.LogError(ex, "DB error creating incident. Idem={Idem}, Dedupe={Dedupe}", idempotencyKey, dedupeKey);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await repo.Query.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

                if (existing != null) return existing;
            }

            throw;
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        TryTelemetry(() => _telemetry.Created(incident, operatorId), "IncidentCreated", incident.Id);
        _ = TryRealtime(() => _realtime.CreatedAsync(incident, ct), "IncidentCreatedRT", incident.Id);

        return incident;
    }

    public async Task AssignAsync(int incidentId, Guid userId, CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        var incident = await repo.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException("Incident not found");

        incident.Assign(userId, _clock.UtcNow);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                aggregateType: "incident",
                aggregateId: incident.Id.ToString(),
                topic: "incident",
                @event: "assigned",
                scope: "incident",
                scopeKey: incident.Id.ToString(),
                payload: new { IncidentId = incident.Id, UserId = userId, Status = incident.Status.ToString(), AssignedAt = incident.AssignedAt },
                idempotencyKey: $"incident.assigned:{incident.Id}:{userId}",
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        TryTelemetry(() => _telemetry.Assigned(incidentId, userId), "IncidentAssigned", incidentId);
        _ = TryRealtime(() => _realtime.AssignedAsync(incident, userId, ct), "IncidentAssignedRT", incidentId);
    }

    public async Task StartWorkAsync(int incidentId, Guid actorUserId, CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        var incident = await repo.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException("Incident not found");

        incident.StartWork(actorUserId, _clock.UtcNow);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                "incident",
                incident.Id.ToString(),
                "incident",
                "status_changed",
                scope: "incident",
                scopeKey: incident.Id.ToString(),
                payload: new { IncidentId = incident.Id, Status = incident.Status.ToString(), StartedAt = incident.StartedAt },
                idempotencyKey: $"incident.status:{incident.Id}:{incident.Status}",
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        TryTelemetry(() => _telemetry.InProgress(incidentId, actorUserId), "IncidentInProgress", incidentId);
        _ = TryRealtime(() => _realtime.StatusChangedAsync(incident, ct), "IncidentStatusChangedRT", incidentId);
    }

    public async Task ResolveAsync(int incidentId, Guid actorUserId, CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        var incident = await repo.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException("Incident not found");

        incident.Resolve(actorUserId, _clock.UtcNow);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                "incident",
                incident.Id.ToString(),
                "incident",
                "status_changed",
                scope: "incident",
                scopeKey: incident.Id.ToString(),
                payload: new { IncidentId = incident.Id, Status = incident.Status.ToString(), ResolvedAt = incident.ResolvedAt },
                idempotencyKey: $"incident.status:{incident.Id}:{incident.Status}",
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        var resolutionSeconds = incident.ResolvedAt is null
            ? 0
            : incident.ResolvedAt.Value.Subtract(incident.Timestamp).TotalSeconds;

        TryTelemetry(() => _telemetry.Resolved(incident, actorUserId, resolutionSeconds), "IncidentResolved", incidentId);
        _ = TryRealtime(() => _realtime.StatusChangedAsync(incident, ct), "IncidentStatusChangedRT", incidentId);
    }

    public async Task CloseAsync(int incidentId, CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        var incident = await repo.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException("Incident not found");

        incident.Close(_clock.UtcNow);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                "incident",
                incident.Id.ToString(),
                "incident",
                "closed",
                scope: "incident",
                scopeKey: incident.Id.ToString(),
                payload: new { IncidentId = incident.Id, Status = incident.Status.ToString(), ClosedAt = incident.ClosedAt },
                idempotencyKey: $"incident.closed:{incident.Id}",
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        TryTelemetry(() => _telemetry.Closed(incidentId), "IncidentClosed", incidentId);
        _ = TryRealtime(() => _realtime.ClosedAsync(incident, ct), "IncidentClosedRT", incidentId);
    }

    public async Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        return await repo.Query.AsNoTracking()
            .Where(x => x.Status != IncidentStatus.Closed)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Incident>> GetByOperatorAsync(int operatorId, CancellationToken ct)
    {
        var repo = _uow.GetRepository<Incident, int>();
        return await repo.Query.AsNoTracking()
            .Where(x => x.OperatorId == operatorId)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    private void TryTelemetry(Action action, string op, int incidentId)
    {
        try { action(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telemetry failed. Op={Op}, IncidentId={IncidentId}", op, incidentId);
        }
    }

    private Task TryRealtime(Func<Task> action, string op, int incidentId)
        => Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Realtime failed. Op={Op}, IncidentId={IncidentId}", op, incidentId);
            }
        });
=======
﻿using Microsoft.ApplicationInsights;
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
>>>>>>> main
}
