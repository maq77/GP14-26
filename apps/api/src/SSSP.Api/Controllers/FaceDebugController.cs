using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using Sssp.Ai.Face; // for ErrorCode, Face, etc.

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/face/debug")]
    public class FaceDebugController : ControllerBase
    {
        private readonly IAIFaceClient _ai;
        private readonly ILogger<FaceDebugController> _logger;

        public FaceDebugController(
            IAIFaceClient ai,
            ILogger<FaceDebugController> logger)
        {
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("embedding")]
        public async Task<IActionResult> GetEmbedding(
            IFormFile file,
            CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            _logger.LogInformation(
                "Debug embedding: calling AI with file size {Size}",
                bytes.Length);

            var result = await _ai.ExtractEmbeddingAsync(
                bytes,
                cameraId: "debug",
                cancellationToken: ct);

            if (result is null)
            {
                _logger.LogWarning(
                    "Debug embedding: AI returned null response.");

                return StatusCode(500, new
                {
                    Success = false,
                    ErrorMessage = "AI returned null response"
                });
            }

            if (!result.Success || result.ErrorCode != ErrorCode.Unspecified)
            {
                _logger.LogWarning(
                    "Debug embedding: AI error. Success={Success}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                    result.Success,
                    result.ErrorCode,
                    result.ErrorMessage ?? "N/A");

                return StatusCode(500, new
                {
                    result.Success,
                    result.ErrorCode,
                    result.ErrorMessage
                });
            }

            if (!result.FaceDetected || result.Faces.Count == 0)
            {
                _logger.LogInformation(
                    "Debug embedding: no faces detected. FaceDetected={FaceDetected}, Faces={Faces}",
                    result.FaceDetected,
                    result.Faces.Count);

                return Ok(new
                {
                    result.Success,
                    result.FaceDetected,
                    Faces = result.Faces.Count
                });
            }

            // Choose best face: highest quality, then largest bbox
            var candidates = result.Faces
                .Where(f => f.EmbeddingVector != null && f.EmbeddingVector.Count > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                _logger.LogWarning(
                    "Debug embedding: faces found but none with valid embeddings. Faces={Faces}",
                    result.Faces.Count);

                return StatusCode(500, new
                {
                    result.Success,
                    ErrorMessage = "No valid face embeddings returned from AI."
                });
            }

            var bestFace = candidates
                .OrderByDescending(f => f.Quality?.OverallScore ?? 0f)
                .ThenByDescending(f =>
                {
                    var b = f.Bbox;
                    if (b is null) return 0f;
                    return b.W * b.H;
                })
                .First();

            var embedding = bestFace.EmbeddingVector;

            _logger.LogInformation(
                "Debug embedding: best face selected. FaceId={FaceId}, OverallScore={Score:F3}, EmbeddingDim={Dim}, FaceDetected={FaceDetected}",
                bestFace.FaceId,
                bestFace.Quality?.OverallScore ?? 0f,
                embedding.Count,
                result.FaceDetected);

            return Ok(new
            {
                result.Success,
                result.CameraId,
                result.FaceDetected,
                Faces = result.Faces.Count,
                SelectedFaceId = bestFace.FaceId,
                QualityScore = bestFace.Quality?.OverallScore ?? 0f,
                EmbeddingDim = embedding.Count,
                First5Values = embedding.Take(5).ToArray(),
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }
}
