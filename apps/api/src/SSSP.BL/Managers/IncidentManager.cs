using SSSP.DAL.Enums;
using System.Security.Cryptography;
using System.Text;

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

    public IncidentStatus InitialStatus(IncidentSource source)
        => IncidentStatus.Open;

    public string BuildDedupeKey(IncidentType type, IncidentSource source, int? operatorId, DateTime timestampUtc)
    {
        var raw = $"{type}|{source}|{operatorId}|{timestampUtc:yyyyMMddHHmm}";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }
}
