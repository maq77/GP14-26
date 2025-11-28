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
    public class FaceRecognitionService
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
                "HTTP face verify started for camera {CameraId}. Image size {Length} bytes",
                cameraId,
                image?.Length ?? 0);

            var embeddingResult = await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

            if (embeddingResult.Embedding == null || embeddingResult.Embedding.Count == 0)
            {
                _logger.LogWarning(
                    "AI returned empty embedding during verify for camera {CameraId}",
                    cameraId);
                return new FaceMatchResult(false, null, null, 0);
            }

            _logger.LogInformation(
                "AI embedding extracted for verify. Camera {CameraId} Dimension {Dim}",
                cameraId,
                embeddingResult.Embedding.Count);

            var profileRepo = _uow.GetRepository<FaceProfile, Guid>();
            var profiles = await profileRepo.GetAllAsync(ct);

            var result = _matcher.Match(embeddingResult.Embedding, profiles);

            if (result.IsMatch)
            {
                _logger.LogInformation(
                    "Face verified. Camera {CameraId} User {UserId} FaceProfile {FaceProfileId} Similarity {Similarity}",
                    cameraId,
                    result.UserId,
                    result.FaceProfileId,
                    result.Similarity);
            }
            else
            {
                _logger.LogWarning(
                    "Unknown face on verify. Camera {CameraId} BestSimilarity {Similarity}",
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
            _logger.LogInformation(
                "Streaming face verify from embedding for camera {CameraId}. Dimension {Dim}",
                cameraId,
                embedding?.Count ?? 0);

            var profileRepo = _uow.GetRepository<FaceProfile, Guid>();
            var profiles = await profileRepo.GetAllAsync(ct);

            var result = _matcher.Match(embedding, profiles);

            if (result.IsMatch)
            {
                _logger.LogInformation(
                    "Streaming face verified. Camera {CameraId} User {UserId} FaceProfile {FaceProfileId} Similarity {Similarity}",
                    cameraId,
                    result.UserId,
                    result.FaceProfileId,
                    result.Similarity);
            }
            else
            {
                _logger.LogWarning(
                    "Streaming unknown face. Camera {CameraId} BestSimilarity {Similarity}",
                    cameraId,
                    result.Similarity);
            }

            return result;
        }
    }
}
