using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.BL.Helpers.Interfaces
{
    public interface IIncidentTelemetry
    {
        void Created(Incident incident, int? operatorId);
        void Assigned(int incidentId, Guid userId);
        void InProgress(int incidentId, Guid userId);
        void Resolved(Incident incident, Guid userId, double resolutionSeconds);
        void Closed(int incidentId);
    }

}
