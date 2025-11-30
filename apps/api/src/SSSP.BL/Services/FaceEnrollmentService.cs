using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceEnrollmentService : IFaceEnrollmentService
    {
        private readonly IAIFaceClient _ai;
        private readonly IUnitOfWork _uow;
        private readonly IFaceProfileCache _faceProfileCache;
        private readonly ILogger<FaceEnrollmentService> _logger;

        public FaceEnrollmentService(
            IAIFaceClient ai,
            IUnitOfWork uow,
            IFaceProfileCache faceProfileCache,
            ILogger<FaceEnrollmentService> logger)
        {
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _faceProfileCache = faceProfileCache ?? throw new ArgumentNullException(nameof(faceProfileCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FaceProfile> EnrollAsync(
            Guid userId,
            byte[] image,
            string? description,
            CancellationToken ct)
        {
            if (image == null || image.Length == 0)
                throw new ArgumentException("Image is empty.", nameof(image));

            _logger.LogInformation(
                "Face enrollment started. UserId={UserId} ImageSize={Size}",
                userId,
                image.Length);

            var embeddingResult = await _ai.ExtractEmbeddingAsync(
                image,
                cameraId: null,
                cancellationToken: ct);

            if (embeddingResult == null ||
                embeddingResult.Embedding == null ||
                embeddingResult.Embedding.Count == 0 ||
                !embeddingResult.FaceDetected)
            {
                _logger.LogWarning(
                    "Enrollment failed. Invalid embedding from AI. UserId={UserId} FaceDetected={Detected}",
                    userId,
                    embeddingResult?.FaceDetected ?? false);

                throw new InvalidOperationException("No valid embedding returned from AI.");
            }

            var vectorBytes = ToByteArray(embeddingResult.Embedding);
            var profileId = Guid.NewGuid();
            var nowUtc = DateTime.UtcNow;

            var profile = new FaceProfile
            {
                Id = profileId,
                UserId = userId,
                IsPrimary = true,
                Description = description,
                CreatedAt = nowUtc,
                Embeddings =
                {
                    new FaceEmbedding
                    {
                        FaceProfileId = profileId,
                        Vector = vectorBytes,
                        SourceCameraId = null,
                        CreatedAt = nowUtc
                    }
                }
            };

            var repo = _uow.GetRepository<FaceProfile, Guid>();
            await repo.AddAsync(profile, ct);
            await _uow.SaveChangesAsync(ct);

            await _faceProfileCache.InvalidateAsync();

            _logger.LogInformation(
                "Face enrollment completed. UserId={UserId} ProfileId={ProfileId} Dim={Dim} Checksum={Checksum}",
                userId,
                profile.Id,
                embeddingResult.Embedding.Count,
                ComputeChecksum(vectorBytes));

            return profile;
        }

        private static byte[] ToByteArray(System.Collections.Generic.IReadOnlyList<float> embedding)
        {
            var bytes = new byte[embedding.Count * sizeof(float)];
            Buffer.BlockCopy(embedding.ToArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        // lightweight checksum for debugging/drift detection
        private static string ComputeChecksum(byte[] data)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (var i = 0; i < data.Length; i++)
                    hash = (hash ^ data[i]) * 16777619;
                return hash.ToString("X");
            }
        }
    }
}
