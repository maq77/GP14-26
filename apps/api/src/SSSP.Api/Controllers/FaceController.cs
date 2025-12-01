using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Face;
using SSSP.BL.Services;
using SSSP.BL.Services.Interfaces;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaceController : ControllerBase
    {
        private readonly IFaceEnrollmentService _enrollmentService;
        private readonly IFaceRecognitionService _recognitionService;
        private readonly ILogger<FaceController> _logger;

        private const int MAX_IMAGE_SIZE = 10_000_000;

        public FaceController(
            IFaceEnrollmentService enrollmentService,
            IFaceRecognitionService recognitionService,
            ILogger<FaceController> logger)
        {
            _enrollmentService = enrollmentService;
            _recognitionService = recognitionService;
            _logger = logger;
        }

        [HttpPost("enroll")]
        //[Authorize(Roles = "Admin")]
        [RequestSizeLimit(MAX_IMAGE_SIZE)]
        public async Task<IActionResult> Enroll([FromForm] EnrollFaceRequest request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Face enrollment request received. UserId={UserId}, HasDescription={HasDescription}",
                request.UserId, !string.IsNullOrWhiteSpace(request.Description));

            if (request.Image == null || request.Image.Length == 0)
            {
                _logger.LogWarning("Enrollment rejected - missing image. UserId={UserId}", request.UserId);
                return BadRequest(new { Message = "Image is required", UserId = request.UserId });
            }

            if (request.Image.Length > MAX_IMAGE_SIZE)
            {
                _logger.LogWarning("Enrollment rejected - image too large. UserId={UserId}, ImageSize={ImageSize}",
                    request.UserId, request.Image.Length);
                return BadRequest(new { Message = "Image exceeds maximum size", MaxSize = MAX_IMAGE_SIZE, ActualSize = request.Image.Length });
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await request.Image.CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            _logger.LogInformation("Face enrollment image loaded. UserId={UserId}, ImageSize={ImageSize}, ContentType={ContentType}",
                request.UserId, imageBytes.Length, request.Image.ContentType);

            var profile = await _enrollmentService.EnrollAsync(
                request.UserId,
                imageBytes,
                request.Description,
                ct);

            sw.Stop();

            _logger.LogInformation(
                "Face enrollment completed. UserId={UserId}, FaceProfileId={FaceProfileId}, IsPrimary={IsPrimary}, ElapsedMs={ElapsedMs}",
                request.UserId, profile.Id, profile.IsPrimary, sw.ElapsedMilliseconds);

            return Ok(new
            {
                profile.Id,
                profile.UserId,
                profile.Description,
                profile.IsPrimary,
                profile.CreatedAt,
                ElapsedMs = sw.ElapsedMilliseconds
            });
        }

        [HttpPost("verify")]
        [AllowAnonymous]
        [RequestSizeLimit(MAX_IMAGE_SIZE)]
        public async Task<ActionResult<FaceMatchResponse>> Verify([FromForm] VerifyFaceRequest request, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Face verification request received. CameraId={CameraId}",
                request.CameraId ?? "N/A");

            if (request.Image == null || request.Image.Length == 0)
            {
                _logger.LogWarning("Verification rejected - missing image. CameraId={CameraId}",
                    request.CameraId);
                return BadRequest(new { Message = "Image is required", CameraId = request.CameraId });
            }

            if (request.Image.Length > MAX_IMAGE_SIZE)
            {
                _logger.LogWarning("Verification rejected - image too large. CameraId={CameraId}, ImageSize={ImageSize}",
                    request.CameraId, request.Image.Length);
                return BadRequest(new { Message = "Image exceeds maximum size", MaxSize = MAX_IMAGE_SIZE, ActualSize = request.Image.Length });
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await request.Image.CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            _logger.LogInformation("Face verification image loaded. CameraId={CameraId}, ImageSize={ImageSize}, ContentType={ContentType}",
                request.CameraId, imageBytes.Length, request.Image.ContentType);

            var result = await _recognitionService.VerifyAsync(
                imageBytes,
                request.CameraId,
                ct);

            sw.Stop();

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
                    request.CameraId, dto.UserId, dto.FaceProfileId, dto.Similarity, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Face verification completed - NO MATCH. CameraId={CameraId}, BestSimilarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                    request.CameraId, dto.Similarity, sw.ElapsedMilliseconds);
            }

            return Ok(dto);
        }
    }
}