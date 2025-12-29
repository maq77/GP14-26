using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.Api.Realtime;
using SSSP.DAL.Models;

namespace SSSP.Api.Outbox;

public sealed class OutboxPublisher
{
    private readonly INotificationPublisher _rt;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(INotificationPublisher rt, ILogger<OutboxPublisher> logger)
    {
        _rt = rt;
        _logger = logger;
    }

    public async Task PublishAsync(OutboxMessage m, CancellationToken ct)
    {
        // payload is JSON already, we can publish as JsonElement to keep it flexible
        JsonElement payload = JsonDocument.Parse(m.PayloadJson).RootElement;

        switch (m.Scope)
        {
            case "global":
                await _rt.ToGlobalAsync(m.Topic, m.Event, payload, ct);
                return;

            case "user":
                await _rt.ToUserAsync(Guid.Parse(m.ScopeKey!), m.Topic, m.Event, payload, ct);
                return;

            case "operator":
                await _rt.ToOperatorAsync(int.Parse(m.ScopeKey!), m.Topic, m.Event, payload, ct);
                return;

            case "camera":
                await _rt.ToCameraAsync(m.ScopeKey!, m.Topic, m.Event, payload, ct);
                return;

            case "role":
                await _rt.ToRoleAsync(m.ScopeKey!, m.Topic, m.Event, payload, ct);
                return;

            case "incident":
                await _rt.ToIncidentAsync(int.Parse(m.ScopeKey!), m.Topic, m.Event, payload, ct);
                return;

            default:
                _logger.LogWarning("Unknown outbox scope: {Scope}", m.Scope);
                return;
        }
    }
}
