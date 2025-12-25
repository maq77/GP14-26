using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
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
    public sealed class CameraController : ControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly ICameraMonitoringService _monitoring;
        private readonly ILogger<CameraController> _logger;
        private readonly TelemetryClient? _telemetry;

        public CameraController(
            ICameraService cameraService,
            ICameraMonitoringService monitoring,
            ILogger<CameraController> logger,
            TelemetryClient telemetry)
        {
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CameraDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var cameras = await _cameraService.GetAllAsync(ct);

                var dto = cameras.Select(MapToDto).ToList();

                sw.Stop();

                _logger.LogInformation(
                    "Retrieved all cameras. Count={Count}, ElapsedMs={ElapsedMs}",
                    dto.Count, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "GetAll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true);

                return Ok(dto);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "GetAll cameras cancelled. ElapsedMs={ElapsedMs}",
                    sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "GetAll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "GetAll cameras failed. ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "GetAll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CameraDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var camera = await _cameraService.GetByIdAsync(id, ct);

                sw.Stop();

                if (camera == null)
                {
                    _logger.LogWarning(
                        "Camera not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        id, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "GetById",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "NotFound");

                    return NotFound(new { Message = "Camera not found", CameraId = id });
                }

                var dto = MapToDto(camera);

                _logger.LogInformation(
                    "Retrieved camera. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                    id, camera.Name, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "GetById",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: id);

                return Ok(dto);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "GetById cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "GetById",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "GetById failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    id, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "GetById",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(CameraDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateCameraRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { Message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Creating camera. Name={Name}, RtspUrl={RtspUrl}, Capabilities={Capabilities}",
                    request.Name, request.RtspUrl, request.Capabilities);

                var camera = await _cameraService.CreateAsync(
                    request.Name,
                    request.RtspUrl,
                    request.Capabilities,
                    request.RecognitionMode,
                    request.MatchThresholdOverride,
                    ct);

                sw.Stop();

                var dto = MapToDto(camera);

                _logger.LogInformation(
                    "Camera created. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                    camera.Id, camera.Name, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Create",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: camera.Id);

                return CreatedAtAction(nameof(GetById), new { id = camera.Id }, dto);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Create camera cancelled. ElapsedMs={ElapsedMs}",
                    sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Create",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Create camera failed. ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "Create",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCameraRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { Message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Updating camera. CameraId={CameraId}, Name={Name}, IsActive={IsActive}",
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
                    _logger.LogWarning(
                        "Camera update failed - not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        id, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Update",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "NotFound");

                    return NotFound(new { Message = "Camera not found", CameraId = id });
                }

                _logger.LogInformation(
                    "Camera updated. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Update",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: id);

                return NoContent();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Update camera cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Update",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Update camera failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    id, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "Update",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Deleting camera. CameraId={CameraId}",
                    id);

                var deleted = await _cameraService.DeleteAsync(id, ct);

                sw.Stop();

                if (!deleted)
                {
                    _logger.LogWarning(
                        "Camera deletion failed - not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        id, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Delete",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "NotFound");

                    return NotFound(new { Message = "Camera not found", CameraId = id });
                }

                _logger.LogInformation(
                    "Camera deleted. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Delete",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: id);

                return NoContent();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Delete camera cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Delete",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Delete camera failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    id, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "Delete",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpPost("{id:int}/start")]
        [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Start(int id, [FromBody] StartCameraRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { Message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Start camera requested. CameraId={CameraId}, CustomRtspUrl={HasCustomUrl}",
                    id, !string.IsNullOrWhiteSpace(request.RtspUrl));

                var camera = await _cameraService.GetByIdAsync(id, ct);

                if (camera == null)
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Start failed - camera not found. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        id, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Start",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "NotFound");

                    return NotFound(new { Message = "Camera not found", CameraId = id });
                }

                if (!camera.IsActive)
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Start failed - camera inactive. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                        id, camera.Name, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Start",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "Inactive");

                    return BadRequest(new
                    {
                        Message = "Camera is inactive",
                        CameraId = id,
                        Name = camera.Name
                    });
                }

                var rtspUrl = string.IsNullOrWhiteSpace(request.RtspUrl)
                    ? camera.RtspUrl
                    : request.RtspUrl;

                var started = await _monitoring.StartAsync(camera.Id, rtspUrl, ct);

                sw.Stop();

                if (!started)
                {
                    _logger.LogWarning(
                        "Start rejected - camera already running. CameraId={CameraId}, Name={Name}, ElapsedMs={ElapsedMs}",
                        camera.Id, camera.Name, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Start",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "AlreadyRunning");

                    return Conflict(new
                    {
                        Message = "Camera is already being monitored",
                        CameraId = camera.Id,
                        Name = camera.Name
                    });
                }

                _logger.LogInformation(
                    "Camera monitoring started. CameraId={CameraId}, Name={Name}, RtspUrl={RtspUrl}, ElapsedMs={ElapsedMs}",
                    camera.Id, camera.Name, rtspUrl, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Start",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: id);

                return Accepted(new
                {
                    camera.Id,
                    camera.Name,
                    RtspUrl = rtspUrl,
                    StartedAt = DateTimeOffset.UtcNow
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Start camera cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Start",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Start camera failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    id, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "Start",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpPost("{id:int}/stop")]
        [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Stop(int id, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Stop camera requested. CameraId={CameraId}",
                    id);

                var stopped = await _monitoring.StopAsync(id, ct);

                sw.Stop();

                if (!stopped)
                {
                    _logger.LogWarning(
                        "Stop rejected - camera not running. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                        id, sw.ElapsedMilliseconds);

                    TrackCameraApiMetric(
                        operation: "Stop",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        cameraId: id,
                        errorReason: "NotRunning");

                    return NotFound(new { Message = "Camera is not being monitored", CameraId = id });
                }

                _logger.LogInformation(
                    "Camera monitoring stopped. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Stop",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    cameraId: id);

                return Accepted(new { CameraId = id, StoppedAt = DateTimeOffset.UtcNow });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Stop camera cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);

                TrackCameraApiMetric(
                    operation: "Stop",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Stop camera failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    id, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackCameraApiMetric(
                    operation: "Stop",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    cameraId: id,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpGet("active")]
        [ProducesResponseType(typeof(IEnumerable<CameraMonitoringStatus>), StatusCodes.Status200OK)]
        public IActionResult GetActiveSessions()
        {
            var sw = Stopwatch.StartNew();

            var sessions = _monitoring.GetActiveSessions();

            sw.Stop();

            _logger.LogInformation(
                "Retrieved active camera sessions. Count={Count}, ElapsedMs={ElapsedMs}",
                sessions.Count, sw.ElapsedMilliseconds);

            TrackCameraApiMetric(
                operation: "GetActiveSessions",
                elapsedMs: sw.ElapsedMilliseconds,
                success: true);

            return Ok(sessions);
        }

        #region Private Helpers

        private static CameraDTO MapToDto(SSSP.DAL.Models.Camera camera)
        {
            return new CameraDTO
            {
                Id = camera.Id,
                Name = camera.Name,
                RtspUrl = camera.RtspUrl,
                IsActive = camera.IsActive,
                Capabilities = camera.Capabilities,
                RecognitionMode = camera.RecognitionMode,
                MatchThresholdOverride = camera.MatchThresholdOverride
            };
        }

        private void TrackCameraApiMetric(
            string operation,
            long elapsedMs,
            bool success,
            int? cameraId = null,
            string? errorReason = null)
        {
            if (_telemetry is null)
                return;

            var props = new Dictionary<string, string>
            {
                ["Operation"] = operation,
                ["Success"] = success.ToString()
            };

            if (cameraId.HasValue)
            {
                props["CameraId"] = cameraId.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(errorReason))
            {
                props["ErrorReason"] = errorReason;
            }

            _telemetry.TrackMetric("CameraApiLatencyMs", elapsedMs, props);
        }

        #endregion
    }
}
