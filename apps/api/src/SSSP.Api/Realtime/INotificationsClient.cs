using SSSP.Api.DTOs.Incidents;
using SSSP.Api.Realtime.Contracts;
using SSSP.Api.Realtime.Contracts.Cameras;
using SSSP.Api.Realtime.Contracts.Faces;
using SSSP.Api.Realtime.Contracts.Incidents;
using SSSP.Api.Realtime.Contracts.Sensors;
using SSSP.Api.Realtime.Contracts.System;

namespace SSSP.Api.Realtime;

public interface INotificationsClient
{
    // Raw pipe (always works)
    Task Receive(RealtimeEnvelope msg);

    // Optional JS quick pipe
    Task Event(string name, RealtimeEnvelope msg);

    // Typed pipes
    Task ReceiveIncident(RealtimeEnvelope<IncidentResponse> msg);

    Task ReceiveIncidentAssigned(RealtimeEnvelope<IncidentAssignedPayload> msg);

    Task ReceiveCameraStatus(RealtimeEnvelope<CameraStatusPayload> msg);
    Task ReceiveCameraTracking(RealtimeEnvelope<CameraTrackingPayload> msg);

    Task ReceiveSensorAlert(RealtimeEnvelope<SensorAlertPayload> msg);

    Task ReceiveFaceRecognized(RealtimeEnvelope<FaceRecognizedPayload> msg);

    Task ReceiveSystem(RealtimeEnvelope<SystemNotificationPayload> msg);
}
