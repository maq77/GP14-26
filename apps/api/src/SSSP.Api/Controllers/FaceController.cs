using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Face;
using SSSP.BL.Services;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaceController : ControllerBase
    {
        private readonly FaceEnrollmentService _enrollmentService;
        private readonly FaceRecognitionService _recognitionService;
        private readonly ILogger<FaceController> _logger;

        public FaceController(
            FaceEnrollmentService enrollmentService,
            FaceRecognitionService recognitionService,
            ILogger<FaceController> logger)
        {
            _enrollmentService = enrollmentService;
            _recognitionService = recognitionService;
            _logger = logger;
        }

        [HttpPost("enroll")]
        //[Authorize(Roles = "Admin")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Enroll(
            [FromForm] EnrollFaceRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP face enroll request received for user {UserId}",
                request.UserId);

            if (request.Image == null || request.Image.Length == 0)
            {
                _logger.LogWarning(
                    "Face enroll request missing image for user {UserId}",
                    request.UserId);
                return BadRequest("Image is required");
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await request.Image.CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            _logger.LogInformation(
                "Face enroll image loaded for user {UserId}, size {Length} bytes",
                request.UserId,
                imageBytes.Length);

            var profile = await _enrollmentService.EnrollAsync(
                request.UserId,
                imageBytes,
                request.Description,
                ct);

            _logger.LogInformation(
                "Face enroll completed for user {UserId}, face profile {FaceProfileId}",
                request.UserId,
                profile.Id);

            return Ok(new
            {
                profile.Id,
                profile.UserId,
                profile.Description,
                profile.IsPrimary,
                profile.CreatedAt
            });
        }

        [HttpPost("verify")]
        [AllowAnonymous]
        [RequestSizeLimit(10_000_000)]
        public async Task<ActionResult<FaceMatchResponse>> Verify(
            [FromForm] VerifyFaceRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP face verify request received from camera {CameraId}",
                request.CameraId);

            if (request.Image == null || request.Image.Length == 0)
            {
                _logger.LogWarning(
                    "Face verify request missing image for camera {CameraId}",
                    request.CameraId);
                return BadRequest("Image is required");
            }

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await request.Image.CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            _logger.LogInformation(
                "Face verify image loaded from camera {CameraId}, size {Length} bytes",
                request.CameraId,
                imageBytes.Length);

            var result = await _recognitionService.VerifyAsync(
                imageBytes,
                request.CameraId,
                ct);

            var dto = new FaceMatchResponse
            {
                IsMatch = result.IsMatch,
                UserId = result.UserId,
                FaceProfileId = result.FaceProfileId,
                Similarity = result.Similarity
            };

            _logger.LogInformation(
                "HTTP face verify result for camera {CameraId}. IsMatch {IsMatch} UserId {UserId} Similarity {Similarity}",
                request.CameraId,
                dto.IsMatch,
                dto.UserId,
                dto.Similarity);

            return Ok(dto);
        }

    }
}
