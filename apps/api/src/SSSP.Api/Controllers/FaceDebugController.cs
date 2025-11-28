using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Infrastructure.AI.Grpc.Interfaces;

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
            _ai = ai;
            _logger = logger;
        }

        [HttpPost("embedding")]
        public async Task<IActionResult> GetEmbedding(
            IFormFile file,
            CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            _logger.LogInformation(
                "Debug embedding: calling AI with file size {Size}",
                bytes.Length);

            var result = await _ai.ExtractEmbeddingAsync(bytes, cameraId: "debug");

            if (!result.Success || result.Embedding == null || result.Embedding.Count == 0)
            {
                _logger.LogWarning(
                    "Debug embedding: AI returned no embedding. Success={Success} Error={Error}",
                    result.Success,
                    result.ErrorMessage);

                return StatusCode(500, new
                {
                    result.Success,
                    result.ErrorMessage
                });
            }

            _logger.LogInformation(
                "Debug embedding: got embedding_dim={Dim} face_detected={FaceDetected}",
                result.Embedding.Count,
                result.Success);

            return Ok(new
            {
                result.Success,
                //result.Success,
                EmbeddingDim = result.Embedding.Count,
                First5Values = result.Embedding.Take(5).ToArray()
            });
        }
    }
}
