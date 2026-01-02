using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Telemetry.Abstractions.Incidents
{
    public sealed class IncidentTelemetry : IIncidentTelemetry
    {
        private readonly TelemetryClient _telemetry;

        public IncidentTelemetry(TelemetryClient telemetry) => _telemetry = telemetry;

        public void Created(Incident incident, int? operatorId)
        {
            _telemetry.TrackEvent("IncidentCreated", new Dictionary<string, string>
            {
                ["IncidentId"] = incident.Id.ToString(),
                ["Type"] = incident.Type.ToString(),
                ["Severity"] = incident.Severity.ToString(),
                ["Source"] = incident.Source.ToString(),
                ["OperatorId"] = operatorId?.ToString() ?? "N/A"
            });

            _telemetry.TrackMetric("IncidentCreatedCount", 1,
                new Dictionary<string, string>
                {
                    ["Severity"] = incident.Severity.ToString(),
                    ["Source"] = incident.Source.ToString()
                });
        }

        public void Assigned(int incidentId, Guid userId) =>
            _telemetry.TrackEvent("IncidentAssigned", new Dictionary<string, string>
            {
                ["IncidentId"] = incidentId.ToString(),
                ["UserId"] = userId.ToString()
            });

        public void InProgress(int incidentId, Guid userId) =>
            _telemetry.TrackEvent("IncidentInProgress", new Dictionary<string, string>
            {
                ["IncidentId"] = incidentId.ToString(),
                ["UserId"] = userId.ToString()
            });

        public void Resolved(Incident incident, Guid userId, double resolutionSeconds)
        {
            _telemetry.TrackEvent("IncidentResolved", new Dictionary<string, string>
            {
                ["IncidentId"] = incident.Id.ToString(),
                ["UserId"] = userId.ToString()
            });

            _telemetry.TrackMetric("IncidentResolutionSeconds", resolutionSeconds,
                new Dictionary<string, string> { ["Severity"] = incident.Severity.ToString() });
        }

        public void Closed(int incidentId) =>
            _telemetry.TrackEvent("IncidentClosed", new Dictionary<string, string> { ["IncidentId"] = incidentId.ToString() });
    }
}
