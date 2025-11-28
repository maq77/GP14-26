using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Managers;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceRecognitionService
    {
        private readonly IAIFaceClient _ai;
        private readonly IUnitOfWork _uow;
        private readonly FaceMatchingManager _matcher;
        private readonly ILogger<FaceRecognitionService> _logger;

        public FaceRecognitionService(
            IAIFaceClient ai,
            IUnitOfWork uow,
            FaceMatchingManager matcher,
            ILogger<FaceRecognitionService> logger)
        {
            _ai = ai;
            _uow = uow;
            _matcher = matcher;
            _logger = logger;
        }

        public async Task<FaceMatchResult> VerifyAsync(
            byte[] image,
            string cameraId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Face verify started. Camera={CameraId} ImageSize={Size}",
                cameraId,
                image?.Length ?? 0);

            var embeddingResult =
                await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

            if (embeddingResult.Embedding == null ||
                embeddingResult.Embedding.Count == 0)
            {
                _logger.LogWarning(
                    "Verify failed. Empty embedding from AI. Camera={CameraId}",
                    cameraId);
                return new FaceMatchResult(false, null, null, 0);
            }

            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var profiles = await repo.GetAllAsync(ct);

            var result = _matcher.Match(
                embeddingResult.Embedding,
                profiles);

            if (result.IsMatch)
            {
                _logger.LogInformation(
                    "Face verified. Camera={CameraId} User={UserId} Similarity={Similarity}",
                    cameraId,
                    result.UserId,
                    result.Similarity);
            }
            else
            {
                _logger.LogInformation(
                    "Unknown face. Camera={CameraId} BestSimilarity={Similarity}",
                    cameraId,
                    result.Similarity);
            }

            return result;
        }

        public async Task<FaceMatchResult> VerifyEmbeddingAsync(
            System.Collections.Generic.IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct)
        {
            if (embedding == null || embedding.Count == 0)
            {
                _logger.LogDebug(
                    "Streaming verify skipped. Empty embedding. Camera={CameraId}",
                    cameraId);
                return new FaceMatchResult(false, null, null, 0);
            }

            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var profiles = await repo.GetAllAsync(ct);

            var result = _matcher.Match(embedding, profiles);

            if (result.IsMatch)
            {
                _logger.LogInformation(
                    "Streaming verified. Camera={CameraId} User={UserId} Similarity={Similarity}",
                    cameraId,
                    result.UserId,
                    result.Similarity);
            }
            else
            {
                _logger.LogDebug(
                    "Streaming unknown face. Camera={CameraId} BestSimilarity={Similarity}",
                    cameraId,
                    result.Similarity);
            }

            return result;
        }
    }
}
