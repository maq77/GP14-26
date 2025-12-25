using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public class SensorService : ISensorService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<SensorService> _logger;

        public SensorService(IUnitOfWork uow, ILogger<SensorService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Sensor>> GetAllAsync(CancellationToken ct)
        {
            var repo = _uow.GetRepository<Sensor, int>();
            var sensors = await repo.GetAllAsync(ct);
            return sensors as IReadOnlyList<Sensor> ?? new List<Sensor>(sensors);
        }

        public async Task<Sensor?> GetByIdAsync(int id, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Sensor, int>();
            return await repo.GetByIdAsync(id, ct);
        }

        public async Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Sensor, int>();
            sensor.CreatedAt = DateTime.UtcNow;
            sensor.IsActive = true;

            await repo.AddAsync(sensor, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Sensor {Id} created", sensor.Id);
            return sensor;
        }

        public async Task<bool> UpdateAsync(int id, Sensor updated, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Sensor, int>();
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null)
                return false;

            existing.Name = updated.Name;
            existing.OperatorId = updated.OperatorId;
            existing.Type = updated.Type;
            existing.Location = updated.Location;
            existing.IsActive = updated.IsActive;
            existing.LastReadingAt = updated.LastReadingAt;

            await repo.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Sensor {Id} updated", id);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Sensor, int>();
            var existing = await repo.GetByIdAsync(id, ct);
            if (existing == null)
                return false;

            await repo.DeleteAsync(id, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Sensor {Id} deleted", id);
            return true;
        }
    }
}
