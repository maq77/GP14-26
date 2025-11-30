using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class CameraService : ICameraService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<CameraService> _logger;

        public CameraService(
            IUnitOfWork uow,
            ILogger<CameraService> logger)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<Camera>> GetAllAsync(CancellationToken ct)
        {
            var repo = _uow.GetRepository<Camera, int>();
            var cameras = (await repo.GetAllAsync(ct)).ToList();

            _logger.LogDebug("Loaded {Count} cameras from database", cameras.Count);

            return cameras;
        }


        public async Task<Camera?> GetByIdAsync(int id, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Camera, int>();
            var camera = await repo.GetByIdAsync(id, ct);

            if (camera == null)
            {
                _logger.LogWarning("Camera {CameraId} not found", id);
            }

            return camera;
        }

        public async Task<Camera> CreateAsync(
            string name,
        string rtspUrl,
            CameraAICapabilities capabilities,
            CameraRecognitionMode recognitionMode,
            double? matchThresholdOverride,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<Camera, int>();

            var camera = new Camera
            {
                Name = name,
                RtspUrl = rtspUrl,
                IsActive = true,
                Capabilities = capabilities,
                RecognitionMode = recognitionMode,
                MatchThresholdOverride = matchThresholdOverride
            };

            await repo.AddAsync(camera, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Camera created. Id={CameraId} Name={Name} Capabilities={Capabilities} Mode={Mode}",
                camera.Id,
                camera.Name,
                camera.Capabilities,
                camera.RecognitionMode);

            return camera;
        }

        public async Task<bool> UpdateAsync(
            int id,
            string name,
            string rtspUrl,
        bool isActive,
            CameraAICapabilities capabilities,
            CameraRecognitionMode recognitionMode,
            double? matchThresholdOverride,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<Camera, int>();
            var camera = await repo.GetByIdAsync(id, ct);

            if (camera == null)
            {
                _logger.LogWarning("Camera {CameraId} not found for update", id);
                return false;
            }

            camera.Name = name;
            camera.RtspUrl = rtspUrl;
            camera.IsActive = isActive;
            camera.Capabilities = capabilities;
            camera.RecognitionMode = recognitionMode;
            camera.MatchThresholdOverride = matchThresholdOverride;

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Camera updated. Id={CameraId} Name={Name} Capabilities={Capabilities} Mode={Mode} IsActive={IsActive}",
                camera.Id,
                camera.Name,
                camera.Capabilities,
                camera.RecognitionMode,
                camera.IsActive);

            return true;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var repo = _uow.GetRepository<Camera, int>();
            var camera = await repo.GetByIdAsync(id, ct);

            if (camera == null)
            {
                _logger.LogWarning("Camera {CameraId} not found for delete", id);
                return false;
            }

            await repo.DeleteAsync(id, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Camera deleted. Id={CameraId} Name={Name}",
                camera.Id,
                camera.Name);

            return true;
        }
    }
}
