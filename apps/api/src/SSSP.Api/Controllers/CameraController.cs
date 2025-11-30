using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Camera;
using SSSP.Api.DTOs.Grpc;
using SSSP.BL.Services.Interfaces;

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
            var sw = Stopwatch.StartNew();

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
            }).ToList();

            sw.Stop();

            _logger.LogInformation("Retrieved all cameras. Count={Count}, ElapsedMs={ElapsedMs}",
                dto.Count, sw.ElapsedMilliseconds);

            return Ok(dto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            var camera = await _cameraService.GetByIdAsync(id, ct);

            sw.Stop();

            if (camera == null)
            {
                _logger.LogWarning("Camera not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return NotFound(new { Message = "Camera not found", CameraId = id });
            }

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

            _logger.LogInformation("Retrieved camera. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                id, camera.Name, sw.ElapsedMilliseconds);

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCameraRequest request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Creating camera. Name={Name}, RtspUrl={RtspUrl}, Capabilities={Capabilities}",
                request.Name, request.RtspUrl, request.Capabilities);

            var camera = await _cameraService.CreateAsync(
                request.Name,
                request.RtspUrl,
                request.Capabilities,
                request.RecognitionMode,
                request.MatchThresholdOverride,
                ct);

            sw.Stop();

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

            _logger.LogInformation("Camera created. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                camera.Id, camera.Name, sw.ElapsedMilliseconds);

            return CreatedAtAction(nameof(GetById), new { id = camera.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCameraRequest request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Updating camera. CameraId={CameraId}, Name={Name}, IsActive={IsActive}",
                id, request.Name, request.IsActive);

            var updated = await _cameraService.UpdateAsync(
                id,
                request.Name,
                request.RtspUrl,
                request.IsActive,
                request.Capabilities,
                request.RecognitionMode,
                request.MatchThresholdOverride,
                ct);

            sw.Stop();

            if (!updated)
            {
                _logger.LogWarning("Camera update failed - not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return NotFound(new { Message = "Camera not found", CameraId = id });
            }

            _logger.LogInformation("Camera updated. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                id, sw.ElapsedMilliseconds);

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Deleting camera. CameraId={CameraId}", id);

            var deleted = await _cameraService.DeleteAsync(id, ct);

            sw.Stop();

            if (!deleted)
            {
                _logger.LogWarning("Camera deletion failed - not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return NotFound(new { Message = "Camera not found", CameraId = id });
            }

            _logger.LogInformation("Camera deleted. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                id, sw.ElapsedMilliseconds);

            return NoContent();
        }

        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> Start(int id, [FromBody] StartCameraRequest request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Start camera requested. CameraId={CameraId}, CustomRtspUrl={HasCustomUrl}",
                id, !string.IsNullOrWhiteSpace(request.RtspUrl));

            var camera = await _cameraService.GetByIdAsync(id, ct);

            if (camera == null)
            {
                sw.Stop();
                _logger.LogWarning("Start failed - camera not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return NotFound(new { Message = "Camera not found", CameraId = id });
            }

            if (!camera.IsActive)
            {
                sw.Stop();
                _logger.LogWarning("Start failed - camera inactive. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                    id, camera.Name, sw.ElapsedMilliseconds);
                return BadRequest(new { Message = "Camera is inactive", CameraId = id, Name = camera.Name });
            }

            var rtspUrl = string.IsNullOrWhiteSpace(request.RtspUrl) ? camera.RtspUrl : request.RtspUrl;

            var started = await _monitoring.StartAsync(camera.Id, rtspUrl, ct);

            sw.Stop();

            if (!started)
            {
                _logger.LogWarning("Start rejected - camera already running. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                    camera.Id, camera.Name, sw.ElapsedMilliseconds);
                return Conflict(new { Message = "Camera is already being monitored", CameraId = camera.Id, Name = camera.Name });
            }

            _logger.LogInformation("Camera monitoring started. CameraId={CameraId}, Name={Name}, RtspUrl={RtspUrl}, ElapsedMs={ElapsedMs}",
                camera.Id, camera.Name, rtspUrl, sw.ElapsedMilliseconds);

            return Accepted(new
            {
                camera.Id,
                camera.Name,
                RtspUrl = rtspUrl,
                StartedAt = System.DateTimeOffset.UtcNow
            });
        }

        [HttpPost("{id:int}/stop")]
        public async Task<IActionResult> Stop(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Stop camera requested. CameraId={CameraId}", id);

            var stopped = await _monitoring.StopAsync(id, ct);

            sw.Stop();

            if (!stopped)
            {
                _logger.LogWarning("Stop rejected - camera not running. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return NotFound(new { Message = "Camera is not being monitored", CameraId = id });
            }

            _logger.LogInformation("Camera monitoring stopped. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                id, sw.ElapsedMilliseconds);

            return Accepted(new { CameraId = id, StoppedAt = System.DateTimeOffset.UtcNow });
        }

        [HttpGet("active")]
        public IActionResult GetActiveSessions()
        {
            var sw = Stopwatch.StartNew();

            var sessions = _monitoring.GetActiveSessions();

            sw.Stop();

            _logger.LogInformation("Retrieved active camera sessions. Count={Count}, ElapsedMs={ElapsedMs}",
                sessions.Count, sw.ElapsedMilliseconds);

            return Ok(sessions);
        }
    }
}