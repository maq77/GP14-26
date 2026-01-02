using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
<<<<<<< HEAD
using System.Linq;
=======
>>>>>>> main
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Face;
using SSSP.BL.Interfaces;
<<<<<<< HEAD
using SSSP.BL.Services.Interfaces;
using SSSP.Telemetry.Abstractions.Faces;
=======
using SSSP.BL.Monitoring;
using SSSP.BL.Services.Interfaces;
>>>>>>> main

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("face-api")]
    public sealed class FaceController : ControllerBase
    {
        private const int MaxImageSizeBytes = 10_000_000;

        private readonly IFaceEnrollmentService _enrollmentService;
        private readonly IFaceRecognitionService _recognitionService;
        private readonly IFaceProfileCache _faceProfileCache;
<<<<<<< HEAD
        private readonly ILogger<FaceController> _logger;
        private readonly TelemetryClient? _telemetry;      // ApplicationInsights (optional)
        private readonly IFaceMetrics _metrics;            // Prometheus
=======
        private readonly FaceProfileCacheMetrics _faceCacheMetrics;
        private readonly ILogger<FaceController> _logger;
        private readonly TelemetryClient? _telemetry;
>>>>>>> main

        public FaceController(
            IFaceEnrollmentService enrollmentService,
            IFaceRecognitionService recognitionService,
            ILogger<FaceController> logger,
            IFaceProfileCache faceProfileCache,
<<<<<<< HEAD
            IFaceMetrics metrics,
            TelemetryClient? telemetry = null)
=======
            FaceProfileCacheMetrics faceCacheMetrics,
            TelemetryClient telemetry)
>>>>>>> main
        {
            _enrollmentService = enrollmentService ?? throw new ArgumentNullException(nameof(enrollmentService));
            _recognitionService = recognitionService ?? throw new ArgumentNullException(nameof(recognitionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
<<<<<<< HEAD
            _faceProfileCache = faceProfileCache ?? throw new ArgumentNullException(nameof(faceProfileCache));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _telemetry = telemetry; // allow null
=======
            _faceProfileCache = faceProfileCache;
            _faceCacheMetrics = faceCacheMetrics;
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
>>>>>>> main
        }

        // POST: api/face/enroll
        //[Authorize(Roles = "Admin")]
        [HttpPost("enroll")]
        [RequestSizeLimit(MaxImageSizeBytes)]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Enroll([FromForm] EnrollFaceRequest request, CancellationToken ct)
        {
<<<<<<< HEAD
            if (request is null) return BadRequest(new { Message = "Request body is required." });
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var sw = Stopwatch.StartNew();

            try
            {
=======
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
>>>>>>> main
                _logger.LogInformation(
                    "Face enrollment request received. UserId={UserId}, HasDescription={HasDescription}",
                    request.UserId, !string.IsNullOrWhiteSpace(request.Description));

                if (!IsValidImage(request.Image, out var imageValidationError))
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Enrollment rejected - invalid image. UserId={UserId}, Reason={Reason}",
                        request.UserId, imageValidationError);

                    TrackFaceApiMetric(
                        operation: "Enroll",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        imageSizeBytes: request.Image?.Length,
                        userId: request.UserId,
                        errorReason: imageValidationError);

                    return BadRequest(new
                    {
                        Message = imageValidationError,
                        UserId = request.UserId,
                        MaxSize = MaxImageSizeBytes,
                        ActualSize = request.Image?.Length
                    });
                }

                var imageBytes = await ReadImageAsync(request.Image!, ct);

                _logger.LogInformation(
                    "Face enrollment image loaded. UserId={UserId}, ImageSize={ImageSize}, ContentType={ContentType}",
                    request.UserId, imageBytes.Length, request.Image!.ContentType);

                var profile = await _enrollmentService.EnrollAsync(
                    request.UserId,
                    imageBytes,
                    request.Description,
                    ct);

                sw.Stop();

                TrackFaceApiMetric(
                    operation: "Enroll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    imageSizeBytes: imageBytes.Length,
                    userId: request.UserId);

                _logger.LogInformation(
                    "Face enrollment completed. UserId={UserId}, FaceProfileId={FaceProfileId}, IsPrimary={IsPrimary}, ElapsedMs={ElapsedMs}",
                    request.UserId, profile.Id, profile.IsPrimary, sw.ElapsedMilliseconds);

<<<<<<< HEAD
                return Ok(new
=======
                var response = new
>>>>>>> main
                {
                    profile.Id,
                    profile.UserId,
                    profile.Description,
                    profile.IsPrimary,
                    profile.CreatedAt,
                    ElapsedMs = sw.ElapsedMilliseconds
<<<<<<< HEAD
                });
=======
                };

                return Ok(response);
>>>>>>> main
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Face enrollment cancelled. UserId={UserId}, ElapsedMs={ElapsedMs}",
                    request.UserId, sw.ElapsedMilliseconds);

                TrackFaceApiMetric(
                    operation: "Enroll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    userId: request.UserId,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Face enrollment failed. UserId={UserId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    request.UserId, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackFaceApiMetric(
                    operation: "Enroll",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    userId: request.UserId,
                    errorReason: ex.GetType().Name);

<<<<<<< HEAD
=======
                // Let your global exception handler / middleware shape the final response
>>>>>>> main
                throw;
            }
        }

        // POST: api/face/verify
        [HttpPost("verify")]
        [AllowAnonymous]
        [RequestSizeLimit(MaxImageSizeBytes)]
        [ProducesResponseType(typeof(FaceMatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
<<<<<<< HEAD
        public async Task<ActionResult<FaceMatchResponse>> Verify([FromForm] VerifyFaceRequest request, CancellationToken ct)
        {
            if (request is null) return BadRequest(new { Message = "Request body is required." });
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var sw = Stopwatch.StartNew();
            var cameraId = request.CameraId ?? "N/A";
            var endpoint = "/api/Face/verify";

            try
            {
                _logger.LogInformation("Face verification request received. CameraId={CameraId}", cameraId);
=======
        public async Task<ActionResult<FaceMatchResponse>> Verify(
            [FromForm] VerifyFaceRequest request,
            CancellationToken ct)
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
            var cameraId = request.CameraId ?? "N/A";

            try
            {
                _logger.LogInformation(
                    "Face verification request received. CameraId={CameraId}",
                    cameraId);
>>>>>>> main

                if (!IsValidImage(request.Image, out var imageValidationError))
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Verification rejected - invalid image. CameraId={CameraId}, Reason={Reason}",
                        cameraId, imageValidationError);

<<<<<<< HEAD
                    // Prometheus
                    _metrics.IncrementVerifyRequests(endpoint, "fail");
                    _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

=======
>>>>>>> main
                    TrackFaceApiMetric(
                        operation: "Verify",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        imageSizeBytes: request.Image?.Length,
                        cameraId: cameraId,
                        errorReason: imageValidationError);

                    return BadRequest(new
                    {
                        Message = imageValidationError,
                        CameraId = request.CameraId,
                        MaxSize = MaxImageSizeBytes,
                        ActualSize = request.Image?.Length
                    });
                }

                var imageBytes = await ReadImageAsync(request.Image!, ct);

                _logger.LogInformation(
                    "Face verification image loaded. CameraId={CameraId}, ImageSize={ImageSize}, ContentType={ContentType}",
                    cameraId, imageBytes.Length, request.Image!.ContentType);

<<<<<<< HEAD
                var result = await _recognitionService.VerifyAsync(imageBytes, request.CameraId, ct);

                sw.Stop();

                // Prometheus
                _metrics.IncrementVerifyRequests(endpoint, "success");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);
                _metrics.ObserveFacesPerRequest(endpoint, 1);


=======
                var result = await _recognitionService.VerifyAsync(
                    imageBytes,
                    request.CameraId,
                    ct);

                sw.Stop();

>>>>>>> main
                TrackFaceApiMetric(
                    operation: "Verify",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    imageSizeBytes: imageBytes.Length,
                    cameraId: cameraId,
                    userId: result.UserId);

                var dto = new FaceMatchResponse
                {
                    IsMatch = result.IsMatch,
                    UserId = result.UserId,
                    FaceProfileId = result.FaceProfileId,
                    Similarity = result.Similarity
                };

                if (result.IsMatch)
                {
                    _logger.LogInformation(
                        "Face verification completed - MATCH. CameraId={CameraId}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                        cameraId, dto.UserId, dto.FaceProfileId, dto.Similarity, sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation(
                        "Face verification completed - NO MATCH. CameraId={CameraId}, BestSimilarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                        cameraId, dto.Similarity, sw.ElapsedMilliseconds);
                }

                return Ok(dto);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

<<<<<<< HEAD
                _metrics.IncrementVerifyRequests(endpoint, "fail");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);
                _metrics.ObserveFacesPerRequest(endpoint, 0);


                _logger.LogWarning("Face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}", cameraId, sw.ElapsedMilliseconds);

                TrackFaceApiMetric(
                    operation: "Verify",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _metrics.IncrementVerifyRequests(endpoint, "fail");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

                _logger.LogError(ex, "Face verification failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    cameraId, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackFaceApiMetric(
                    operation: "Verify",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        // POST: api/face/verify-many
        [HttpPost("verify-many")]
        [AllowAnonymous]
        [RequestSizeLimit(MaxImageSizeBytes)]
        [ProducesResponseType(typeof(IEnumerable<MultiFaceMatchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<MultiFaceMatchResponse>>> VerifyMany([FromForm] VerifyFaceRequest request, CancellationToken ct)
        {
            if (request is null) return BadRequest(new { Message = "Request body is required." });
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var sw = Stopwatch.StartNew();
            var cameraId = request.CameraId ?? "N/A";
            var endpoint = "/api/Face/verify-many";

            try
            {
                _logger.LogInformation("Multi-face verification request received. CameraId={CameraId}", cameraId);

                if (!IsValidImage(request.Image, out var imageValidationError))
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Multi-face verification rejected - invalid image. CameraId={CameraId}, Reason={Reason}",
                        cameraId, imageValidationError);

                    // Prometheus
                    _metrics.IncrementVerifyRequests(endpoint, "fail");
                    _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

                    TrackFaceApiMetric(
                        operation: "VerifyMany",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        imageSizeBytes: request.Image?.Length,
                        cameraId: cameraId,
                        errorReason: imageValidationError);

                    return BadRequest(new
                    {
                        Message = imageValidationError,
                        CameraId = request.CameraId,
                        MaxSize = MaxImageSizeBytes,
                        ActualSize = request.Image?.Length
                    });
                }

                var imageBytes = await ReadImageAsync(request.Image!, ct);

                _logger.LogInformation(
                    "Multi-face verification image loaded. CameraId={CameraId}, ImageSize={ImageSize}, ContentType={ContentType}",
                    cameraId, imageBytes.Length, request.Image!.ContentType);

                var hits = await _recognitionService.VerifyManyAsync(imageBytes, request.CameraId, ct);

                sw.Stop();

                // Prometheus KPI (objective ~200ms)
                _metrics.ObserveFacesPerRequest(endpoint, hits.Count);
                _metrics.IncrementVerifyRequests(endpoint, "success");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

                TrackFaceApiMetric(
                    operation: "VerifyMany",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    imageSizeBytes: imageBytes.Length,
                    cameraId: cameraId,
                    userId: null);

                var response = hits.Select(h => new MultiFaceMatchResponse
                {
                    FaceId = h.FaceId,
                    BoundingBox = h.Bbox,
                    OverallQuality = h.OverallQuality,
                    IsMatch = h.Match.IsMatch,
                    UserId = h.Match.UserId,
                    FaceProfileId = h.Match.FaceProfileId,
                    Similarity = h.Match.Similarity
                }).ToList();

                _logger.LogInformation(
                    "Multi-face verification completed. CameraId={CameraId}, Faces={Faces}, Matches={Matches}, ElapsedMs={ElapsedMs}",
                    cameraId,
                    response.Count,
                    response.Count(x => x.IsMatch),
                    sw.ElapsedMilliseconds);

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _metrics.IncrementVerifyRequests(endpoint, "fail");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

                _logger.LogWarning(
                    "Multi-face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, sw.ElapsedMilliseconds);

                TrackFaceApiMetric(
                    operation: "VerifyMany",
=======
                _logger.LogWarning(
                    "Face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, sw.ElapsedMilliseconds);

                TrackFaceApiMetric(
                    operation: "Verify",
>>>>>>> main
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: "Cancelled");

                throw;
<<<<<<< HEAD
            }
            catch (Exception ex)
            {
                sw.Stop();

                _metrics.IncrementVerifyRequests(endpoint, "fail");
                _metrics.ObserveVerifyDuration(endpoint, sw.Elapsed.TotalMilliseconds);

                _logger.LogError(
                    ex,
                    "Multi-face verification failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    cameraId, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackFaceApiMetric(
                    operation: "VerifyMany",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: ex.GetType().Name);

                throw;
=======
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Face verification failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    cameraId, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackFaceApiMetric(
                    operation: "Verify",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

       
        
        // POST: api/face/verify-many
        [HttpPost("verify-many")]
        [AllowAnonymous]
        [RequestSizeLimit(MaxImageSizeBytes)]
        [ProducesResponseType(typeof(IEnumerable<MultiFaceMatchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<MultiFaceMatchResponse>>> VerifyMany(
            [FromForm] VerifyFaceRequest request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { Message = "Request body is required." });
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
>>>>>>> main
            }
        }

<<<<<<< HEAD
        // GET: api/face/cache-stats
        // Removed FaceProfileCacheMetrics usage; keep simple stats only.
        [HttpGet("cache-stats")]
        public async Task<ActionResult<object>> GetCacheStats(CancellationToken ct)
        {
            var profiles = await _faceProfileCache.GetAllAsync(ct);

            var response = new
            {
                ProfilesCount = profiles.Count
            };

            _logger.LogInformation("Face cache stats requested. Profiles={Profiles}", profiles.Count);
            return Ok(response);
        }

        #region Private Helpers

        private static bool IsValidImage(IFormFile? file, out string errorMessage)
=======
            var sw = Stopwatch.StartNew();
            var cameraId = request.CameraId ?? "N/A";

            try
            {
                _logger.LogInformation(
                    "Multi-face verification request received. CameraId={CameraId}",
                    cameraId);

                if (!IsValidImage(request.Image, out var imageValidationError))
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Multi-face verification rejected - invalid image. CameraId={CameraId}, Reason={Reason}",
                        cameraId, imageValidationError);

                    TrackFaceApiMetric(
                        operation: "VerifyMany",
                        elapsedMs: sw.ElapsedMilliseconds,
                        success: false,
                        imageSizeBytes: request.Image?.Length,
                        cameraId: cameraId,
                        errorReason: imageValidationError);

                    return BadRequest(new
                    {
                        Message = imageValidationError,
                        CameraId = request.CameraId,
                        MaxSize = MaxImageSizeBytes,
                        ActualSize = request.Image?.Length
                    });
                }

                var imageBytes = await ReadImageAsync(request.Image!, ct);

                _logger.LogInformation(
                    "Multi-face verification image loaded. CameraId={CameraId}, ImageSize={ImageSize}, ContentType={ContentType}",
                    cameraId, imageBytes.Length, request.Image!.ContentType);

                // ---- Core multi-face verification ----
                var hits = await _recognitionService.VerifyManyAsync(
                    imageBytes,
                    request.CameraId,
                    ct);

                sw.Stop();

                // Telemetry: we don’t have a single UserId; send N/A
                TrackFaceApiMetric(
                    operation: "VerifyMany",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: true,
                    imageSizeBytes: imageBytes.Length,
                    cameraId: cameraId,
                    userId: null);

                // Map domain hits -> API DTO
                var response = hits.Select(h =>
                {
                    // Assuming FaceRecognitionHit has:
                    //  - int FaceId
                    //  - FaceBoundingBox BoundingBox { X, Y, W, H }
                    //  - FaceMatchResult Match { IsMatch, UserId, FaceProfileId, Similarity }
                    //  - float QualityScore

                    return new MultiFaceMatchResponse
                    {
                        FaceId = h.FaceId,
                        BoundingBox = h.Bbox,
                        OverallQuality = h.OverallQuality,
                        IsMatch = h.Match.IsMatch,
                        UserId = h.Match.UserId,
                        FaceProfileId = h.Match.FaceProfileId,
                        Similarity = h.Match.Similarity
                    };
                }).ToList();

                _logger.LogInformation(
                    "Multi-face verification completed. CameraId={CameraId}, Faces={Faces}, Matches={Matches}, ElapsedMs={ElapsedMs}",
                    cameraId,
                    response.Count,
                    response.Count(x => x.IsMatch),
                    sw.ElapsedMilliseconds);

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                _logger.LogWarning(
                    "Multi-face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, sw.ElapsedMilliseconds);

                TrackFaceApiMetric(
                    operation: "VerifyMany",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: "Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();

                _logger.LogError(
                    ex,
                    "Multi-face verification failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}, ExceptionType={ExceptionType}",
                    cameraId, sw.ElapsedMilliseconds, ex.GetType().Name);

                TrackFaceApiMetric(
                    operation: "VerifyMany",
                    elapsedMs: sw.ElapsedMilliseconds,
                    success: false,
                    imageSizeBytes: request.Image?.Length,
                    cameraId: cameraId,
                    errorReason: ex.GetType().Name);

                throw;
            }
        }

        [HttpGet("cache-stats")]
        public async Task<ActionResult<object>> GetCacheStats(CancellationToken ct)
        {
            var profiles = await _faceProfileCache.GetAllAsync(ct);
            var (l1h, l1m, l2h, l2m, dbLoads) = _faceCacheMetrics.Snapshot();

            var response = new
            {
                ProfilesCount = profiles.Count,
                Metrics = new
                {
                    L1Hits = l1h,
                    L1Misses = l1m,
                    L2Hits = l2h,
                    L2Misses = l2m,
                    DbLoads = dbLoads
                }
            };

            _logger.LogInformation(
                "Face cache stats requested. Profiles={Profiles}, L1(Hit={L1H}, Miss={L1M}), L2(Hit={L2H}, Miss={L2M}), DbLoads={DbLoads}",
                profiles.Count, l1h, l1m, l2h, l2m, dbLoads);

            return Ok(response);
        }


        #region Private Helpers

        private static bool IsValidImage(
            IFormFile? file,
            out string errorMessage)
>>>>>>> main
        {
            if (file is null || file.Length == 0)
            {
                errorMessage = "Image is required.";
                return false;
            }

            if (file.Length > MaxImageSizeBytes)
            {
                errorMessage = "Image exceeds maximum allowed size.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static async Task<byte[]> ReadImageAsync(IFormFile file, CancellationToken ct)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        private void TrackFaceApiMetric(
            string operation,
            long elapsedMs,
            bool success,
            long? imageSizeBytes = null,
            string? cameraId = null,
            Guid? userId = null,
            string? errorReason = null)
        {
<<<<<<< HEAD
            if (_telemetry is null) return;
=======
            if (_telemetry is null)
                return;
>>>>>>> main

            var props = new Dictionary<string, string>
            {
                ["Operation"] = operation,
                ["Success"] = success.ToString(),
                ["CameraId"] = cameraId ?? "N/A",
                ["UserId"] = userId?.ToString() ?? "N/A"
            };

            if (!string.IsNullOrWhiteSpace(errorReason))
<<<<<<< HEAD
                props["ErrorReason"] = errorReason;
=======
            {
                props["ErrorReason"] = errorReason;
            }
>>>>>>> main

            _telemetry.TrackMetric("FaceApiLatencyMs", elapsedMs, props);

            if (imageSizeBytes.HasValue)
<<<<<<< HEAD
                _telemetry.TrackMetric("FaceApiImageSizeBytes", imageSizeBytes.Value, props);
=======
            {
                _telemetry.TrackMetric("FaceApiImageSizeBytes", imageSizeBytes.Value, props);
            }
>>>>>>> main
        }

        #endregion
    }
}
