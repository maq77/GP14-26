namespace SSSP.Api.Realtime.Contracts.Sensors;

public static class SensorTopics
{
    public const string Topic = "sensor";
    public const string Alert = "alert";
    public const string AqiUpdate = "aqi_update";
    public const string HealthRecommendation = "health_recommendation";
}

public sealed record SensorAlertPayload(
    string SensorId,
    string SensorType,
    string Location,
    double Value,
    string Unit,
    string Severity, 
    string Message,
    DateTimeOffset TsUtc
);

public sealed record AqiAlertPayload(
    string RoomId,
    string RoomName,
    int AqiValue,
    string AqiBand, // "Good", "Moderate", "Unhealthy", "Hazardous"
    string Severity,
    List<string> AffectedUserIds,
    string Recommendation,
    DateTimeOffset TsUtc
);