using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Grpc;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly ICameraMonitoringService _monitoring;
        private readonly ILogger<CameraController> _logger;

        public CameraController(
            IUnitOfWork uow,
            ICameraMonitoringService monitoring,
            ILogger<CameraController> logger)
        {
            _uow = uow;
            _monitoring = monitoring;
            _logger = logger;
        }

        [HttpPost("{id:int}/start")]
        //[Authorize(Roles = "Admin,Operator")]
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

            var started = await _monitoring.StartAsync(camera.Id, rtspUrl, ct);

            if (!started)
            {
                _logger.LogWarning(
                    "Camera {CameraId} monitoring is already running",
                    camera.Id);
                return Conflict("Camera is already being monitored");
            }

            _logger.LogInformation(
                "Camera {CameraId} monitoring accepted on RTSP {RtspUrl}",
                camera.Id,
                rtspUrl);

            return Accepted(new
            {
                camera.Id,
                camera.Name,
                RtspUrl = rtspUrl
            });
        }

        [HttpPost("{id:int}/stop")]
        //[Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Stop(
            int id,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP stop camera requested for camera {CameraId}",
                id);

            var stopped = await _monitoring.StopAsync(id, ct);

            if (!stopped)
            {
                _logger.LogWarning(
                    "Stop requested for non-active camera {CameraId}",
                    id);
                return NotFound("Camera is not being monitored");
            }

            _logger.LogInformation(
                "Camera {CameraId} monitoring stop accepted",
                id);

            return Accepted(new { CameraId = id });
        }

        [HttpGet("active")]
        //[Authorize(Roles = "Admin,Operator")]
        public IActionResult Active()
        {
            var sessions = _monitoring.GetActiveSessions();
            return Ok(sessions);
        }
    }
}
