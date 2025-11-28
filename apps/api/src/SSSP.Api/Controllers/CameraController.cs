using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Grpc;
using SSSP.BL.Services;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly CameraMonitoringService _monitoring;
        private readonly ILogger<CameraController> _logger;

        public CameraController(
            IUnitOfWork uow,
            CameraMonitoringService monitoring,
            ILogger<CameraController> logger)
        {
            _uow = uow;
            _monitoring = monitoring;
            _logger = logger;
        }

        [HttpPost("{id:int}/start")]
        [Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Start(
            int id,
            [FromBody] StartCameraRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP start camera requested for camera {CameraId}",
                id);

            var cameraRepo = _uow.GetRepository<Camera, int>();
            var camera = await cameraRepo.GetByIdAsync(id, ct);

            if (camera == null || !camera.IsActive)
            {
                _logger.LogWarning(
                    "Camera {CameraId} not found or inactive",
                    id);
                return NotFound("Camera not found or inactive");
            }

            var rtspUrl = string.IsNullOrWhiteSpace(request.RtspUrl)
                ? camera.RtspUrl
                : request.RtspUrl;

            _logger.LogInformation(
                "Starting background monitoring for camera {CameraId} on RTSP {RtspUrl}",
                camera.Id,
                rtspUrl);

            _ = Task.Run(() =>
                _monitoring.StartCameraAsync(
                    camera.Id.ToString(),
                    rtspUrl,
                    CancellationToken.None));

            return Accepted(new
            {
                camera.Id,
                camera.Name,
                RtspUrl = rtspUrl
            });
        }
    }
}
