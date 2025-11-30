using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Camera;
using SSSP.Api.DTOs.Grpc;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Roles = "Admin,Operator")]
    public class CameraController : ControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly ICameraMonitoringService _monitoring;
        private readonly ILogger<CameraController> _logger;

        public CameraController(
            ICameraService cameraService,
            ICameraMonitoringService monitoring,
            ILogger<CameraController> logger)
        {
            _cameraService = cameraService;
            _monitoring = monitoring;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var cameras = await _cameraService.GetAllAsync(ct);

            var dto = cameras.Select(c => new CameraDTO
            {
                Id = c.Id,
                Name = c.Name,
                RtspUrl = c.RtspUrl,
                IsActive = c.IsActive,
                Capabilities = c.Capabilities,
                RecognitionMode = c.RecognitionMode,
                MatchThresholdOverride = c.MatchThresholdOverride
            });

            return Ok(dto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var camera = await _cameraService.GetByIdAsync(id, ct);

            if (camera == null)
                return NotFound("Camera not found");

            var dto = new CameraDTO
            {
                Id = camera.Id,
                Name = camera.Name,
                RtspUrl = camera.RtspUrl,
                IsActive = camera.IsActive,
                Capabilities = camera.Capabilities,
                RecognitionMode = camera.RecognitionMode,
                MatchThresholdOverride = camera.MatchThresholdOverride
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateCameraRequest request,
            CancellationToken ct)
        {
            var camera = await _cameraService.CreateAsync(
                request.Name,
                request.RtspUrl,
                request.Capabilities,
                request.RecognitionMode,
                request.MatchThresholdOverride,
                ct);

            var dto = new CameraDTO
            {
                Id = camera.Id,
                Name = camera.Name,
                RtspUrl = camera.RtspUrl,
                IsActive = camera.IsActive,
                Capabilities = camera.Capabilities,
                RecognitionMode = camera.RecognitionMode,
                MatchThresholdOverride = camera.MatchThresholdOverride
            };

            return CreatedAtAction(nameof(GetById), new { id = camera.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateCameraRequest request,
            CancellationToken ct)
        {
            var updated = await _cameraService.UpdateAsync(
                id,
                request.Name,
                request.RtspUrl,
                request.IsActive,
                request.Capabilities,
                request.RecognitionMode,
                request.MatchThresholdOverride,
                ct);

            if (!updated)
                return NotFound("Camera not found");

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var deleted = await _cameraService.DeleteAsync(id, ct);

            if (!deleted)
                return NotFound("Camera not found");

            return NoContent();
        }

        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> Start(
            int id,
            [FromBody] StartCameraRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP start camera requested for camera {CameraId}",
                id);

            var camera = await _cameraService.GetByIdAsync(id, ct);

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
        public IActionResult Active()
        {
            var sessions = _monitoring.GetActiveSessions();
            return Ok(sessions);
        }
    }
}
