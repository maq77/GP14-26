using System.Security.Cryptography;
using System.Text;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;

public sealed class IncidentManager : IIncidentManager
{
    public IncidentSeverity ResolveSeverity(IncidentType type) => type switch
    {
        IncidentType.Weapon => IncidentSeverity.Critical,
        IncidentType.UnauthorizedAccess => IncidentSeverity.High,
        IncidentType.Fighting => IncidentSeverity.High,
        IncidentType.Vandalism => IncidentSeverity.Medium,
        IncidentType.AirQuality => IncidentSeverity.Medium,
        IncidentType.Waste => IncidentSeverity.Low,
        _ => IncidentSeverity.Low
    };

    public IncidentStatus InitialStatus(IncidentSource source) => source switch
    {
        IncidentSource.AIDetection => IncidentStatus.Open,
        IncidentSource.Sensor => IncidentStatus.Open,
        IncidentSource.Manual => IncidentStatus.Assigned,
        IncidentSource.CitizenReport => IncidentStatus.Open,
        _ => IncidentStatus.Open
    };

    public string BuildDedupeKey(Incident incident)
    {
        var raw =
            $"{incident.Type}|{incident.Source}|{incident.OperatorId}|{incident.Timestamp:yyyyMMddHH}";

        using var sha = SHA256.Create();
        return Convert.ToHexString(
            sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }
}
