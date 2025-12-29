using System.Text.Json;

namespace SSSP.Api.Realtime;

public interface INotificationPublisher
{
    // Raw JsonElement publishing (used by OutboxDispatcherWorker)
    Task ToGlobalAsync(string topic, string @event, JsonElement data, CancellationToken ct = default);
    Task ToUserAsync(Guid userId, string topic, string @event, JsonElement data, CancellationToken ct = default);
    Task ToOperatorAsync(int operatorId, string topic, string @event, JsonElement data, CancellationToken ct = default);
    Task ToCameraAsync(string cameraId, string topic, string @event, JsonElement data, CancellationToken ct = default);
    Task ToRoleAsync(string role, string topic, string @event, JsonElement data, CancellationToken ct = default);
    Task ToIncidentAsync(int incidentId, string topic, string @event, JsonElement data, CancellationToken ct = default);

    // Convenience overloads (serializes object -> JsonElement)
    Task ToGlobalAsync<T>(string topic, string @event, T data, CancellationToken ct = default);
    Task ToUserAsync<T>(Guid userId, string topic, string @event, T data, CancellationToken ct = default);
    Task ToOperatorAsync<T>(int operatorId, string topic, string @event, T data, CancellationToken ct = default);
    Task ToCameraAsync<T>(string cameraId, string topic, string @event, T data, CancellationToken ct = default);
    Task ToRoleAsync<T>(string role, string topic, string @event, T data, CancellationToken ct = default);
    Task ToIncidentAsync<T>(int incidentId, string topic, string @event, T data, CancellationToken ct = default);
}
