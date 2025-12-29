using SSSP.DAL.Enums;
using SSSP.DAL.Models;

public interface IIncidentManager
{
    IncidentSeverity ResolveSeverity(IncidentType type);
    IncidentStatus InitialStatus(IncidentSource source);
    string BuildDedupeKey(IncidentType type, IncidentSource source, int? operatorId, DateTime timestampUtc);
}
