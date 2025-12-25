using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.DAL.Models;

namespace SSSP.BL.Services.Interfaces
{
    public interface ISensorService
    {
        Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken ct);
        Task<Sensor?> GetByIdAsync(int id, CancellationToken ct);
        Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct);
        Task<bool> UpdateAsync(int id, Sensor sensor, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
    }
}
