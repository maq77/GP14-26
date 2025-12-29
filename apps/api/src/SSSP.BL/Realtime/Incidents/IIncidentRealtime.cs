using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.BL.Realtime.Incidents
{
    public interface IIncidentRealtime
    {
        Task CreatedAsync(Incident incident, CancellationToken ct);
        Task AssignedAsync(Incident incident, Guid assigneeUserId, CancellationToken ct);
        Task StatusChangedAsync(Incident incident, CancellationToken ct);
        Task ClosedAsync(Incident incident, CancellationToken ct);
    }

}
