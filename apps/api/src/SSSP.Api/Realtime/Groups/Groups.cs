namespace SSSP.Api.Realtime.Groups;

public static class RtGroups
{
    public const string Global = "global";

    public static string User(Guid userId) => $"user:{userId:D}";
    public static string Role(string role) => $"role:{role}";
    public static string Operator(int operatorId) => $"operator:{operatorId}";
    public static string Incident(int incidentId) => $"incident:{incidentId}";
    public static string Camera(string cameraId) => $"camera:{cameraId}";
}
