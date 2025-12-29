using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Incidents;
using SSSP.Api.Hubs;
using SSSP.Api.Realtime.Contracts;
using SSSP.Api.Realtime.Contracts.Cameras;
using SSSP.Api.Realtime.Contracts.Faces;
using SSSP.Api.Realtime.Contracts.Incidents;
using SSSP.Api.Realtime.Contracts.Sensors;
using SSSP.Api.Realtime.Contracts.System;
using SSSP.Api.Realtime.Groups;

namespace SSSP.Api.Realtime;

public sealed class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationsHub, INotificationsClient> _hub;
    private readonly ILogger<SignalRNotificationPublisher> _logger;
    private readonly bool _enableTypedRouting;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public SignalRNotificationPublisher(
        IHubContext<NotificationsHub, INotificationsClient> hub,
        ILogger<SignalRNotificationPublisher> logger,
        IConfiguration cfg)
    {
        _hub = hub;
        _logger = logger;
        _enableTypedRouting = cfg.GetValue("Realtime:EnableTypedRouting", true);
    }

    // ---------------------------
    // Raw publishing (JsonElement)
    // ---------------------------
    public Task ToGlobalAsync(string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.Global, topic, @event, data, ct);

    public Task ToUserAsync(Guid userId, string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.User(userId), topic, @event, data, ct);

    public Task ToOperatorAsync(int operatorId, string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.Operator(operatorId), topic, @event, data, ct);

    public Task ToCameraAsync(string cameraId, string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.Camera(cameraId), topic, @event, data, ct);

    public Task ToRoleAsync(string role, string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.Role(role), topic, @event, data, ct);

    public Task ToIncidentAsync(int incidentId, string topic, string @event, JsonElement data, CancellationToken ct = default)
        => SendGroupAsync(RtGroups.Incident(incidentId), topic, @event, data, ct);

    // ---------------------------
    // Convenience overloads (T -> JsonElement)
    // ---------------------------
    public Task ToGlobalAsync<T>(string topic, string @event, T data, CancellationToken ct = default)
        => ToGlobalAsync(topic, @event, SerializeToElement(data), ct);

    public Task ToUserAsync<T>(Guid userId, string topic, string @event, T data, CancellationToken ct = default)
        => ToUserAsync(userId, topic, @event, SerializeToElement(data), ct);

    public Task ToOperatorAsync<T>(int operatorId, string topic, string @event, T data, CancellationToken ct = default)
        => ToOperatorAsync(operatorId, topic, @event, SerializeToElement(data), ct);

    public Task ToCameraAsync<T>(string cameraId, string topic, string @event, T data, CancellationToken ct = default)
        => ToCameraAsync(cameraId, topic, @event, SerializeToElement(data), ct);

    public Task ToRoleAsync<T>(string role, string topic, string @event, T data, CancellationToken ct = default)
        => ToRoleAsync(role, topic, @event, SerializeToElement(data), ct);

    public Task ToIncidentAsync<T>(int incidentId, string topic, string @event, T data, CancellationToken ct = default)
        => ToIncidentAsync(incidentId, topic, @event, SerializeToElement(data), ct);

    // ---------------------------
    // Internals
    // ---------------------------
    private static JsonElement SerializeToElement<T>(T data)
        => JsonSerializer.SerializeToElement(data, JsonOpts);

    private static RealtimeEnvelope WrapRaw(string topic, string @event, JsonElement data)
        => new(topic, @event, data, DateTimeOffset.UtcNow);

    private static RealtimeEnvelope<T> WrapTyped<T>(string topic, string @event, T data)
        => new(topic, @event, data, DateTimeOffset.UtcNow);

    private async Task SendGroupAsync(string group, string topic, string @event, JsonElement data, CancellationToken ct)
    {
        var raw = WrapRaw(topic, @event, data);
        var eventName = $"{topic}.{@event}";
        var clients = _hub.Clients.Group(group);

        try
        {
            await clients.Receive(raw);
            await clients.Event(eventName, raw);

            if (_enableTypedRouting)
                await RouteTypedAsync(clients, topic, @event, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Realtime publish failed. Group={Group}, Topic={Topic}, Event={Event}",
                group, topic, @event);
        }
    }

    private async Task RouteTypedAsync(INotificationsClient clients, string topic, string @event, JsonElement data)
    {
        try
        {
            if (topic == IncidentTopics.Topic)
            {
                if (@event == IncidentTopics.Assigned)
                {
                    var payload = data.Deserialize<IncidentAssignedPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveIncidentAssigned(WrapTyped(topic, @event, payload));
                    return;
                }

                if (@event is IncidentTopics.Created or IncidentTopics.StatusChanged or IncidentTopics.Closed)
                {
                    var payload = data.Deserialize<IncidentResponse>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveIncident(WrapTyped(topic, @event, payload));
                    return;
                }

                return;
            }

            if (topic == CameraTopics.Topic)
            {
                if (@event == CameraTopics.Status)
                {
                    var payload = data.Deserialize<CameraStatusPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveCameraStatus(WrapTyped(topic, @event, payload));
                    return;
                }

                if (@event == CameraTopics.Tracking)
                {
                    var payload = data.Deserialize<CameraTrackingPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveCameraTracking(WrapTyped(topic, @event, payload));
                    return;
                }

                return;
            }

            if (topic == SensorTopics.Topic)
            {
                if (@event == SensorTopics.Alert)
                {
                    var payload = data.Deserialize<SensorAlertPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveSensorAlert(WrapTyped(topic, @event, payload));
                    return;
                }

                return;
            }

            if (topic == FaceTopics.Topic)
            {
                if (@event == FaceTopics.Recognized)
                {
                    var payload = data.Deserialize<FaceRecognizedPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveFaceRecognized(WrapTyped(topic, @event, payload));
                    return;
                }

                return;
            }

            if (topic == SystemTopics.Topic)
            {
                if (@event == SystemTopics.Notification)
                {
                    var payload = data.Deserialize<SystemNotificationPayload>(JsonOpts);
                    if (payload is not null)
                        await clients.ReceiveSystem(WrapTyped(topic, @event, payload));
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Typed routing failed. Topic={Topic}, Event={Event}. Falling back to raw Receive().",
                topic, @event);
        }
    }

}
