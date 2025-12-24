using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;
using Sssp.Ai.Face; // for Face, ErrorCode, etc.

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

            // New multi-face aware embedding call
            var response = await _ai.ExtractEmbeddingAsync(
                image,
                cameraId: null,
                cancellationToken: ct);

            // Validate response
            if (response is null)
            {
                _logger.LogWarning(
                    "Enrollment failed. AI response is null. UserId={UserId}",
                    userId);

                throw new InvalidOperationException("No response from AI face service.");
            }

            if (!response.Success || response.ErrorCode != ErrorCode.Unspecified)
            {
                _logger.LogWarning(
                    "Enrollment failed. AI embedding error. UserId={UserId}, Success={Success}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                    userId,
                    response.Success,
                    response.ErrorCode,
                    response.ErrorMessage ?? "N/A");

                throw new InvalidOperationException(
                    $"Face embedding failed: {response.ErrorMessage ?? response.ErrorCode.ToString()}");
            }

            if (!response.FaceDetected || response.Faces.Count == 0)
            {
                _logger.LogWarning(
                    "Enrollment failed. No faces detected. UserId={UserId}, FaceDetected={FaceDetected}, Faces={Faces}",
                    userId,
                    response.FaceDetected,
                    response.Faces.Count);

                throw new InvalidOperationException("No face detected in enrollment image.");
            }

            // Choose best face:
            // 1) highest quality.overall_score
            // 2) fallback: largest bbox area
            var candidates = response.Faces
                .Where(f => f.EmbeddingVector != null && f.EmbeddingVector.Count > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                _logger.LogWarning(
                    "Enrollment failed. No valid faces with embeddings. UserId={UserId}, Faces={Faces}",
                    userId,
                    response.Faces.Count);

                throw new InvalidOperationException("No valid face embeddings returned from AI.");
            }

            Face bestFace = candidates
                .OrderByDescending(f => f.Quality?.OverallScore ?? 0f)
                .ThenByDescending(f =>
                {
                    var b = f.Bbox;
                    if (b is null)
                        return 0f;
                    return b.W * b.H;
                })
                .First();

            var embedding = bestFace.EmbeddingVector;
            var vectorBytes = ToByteArray(embedding);
            var profileId = Guid.NewGuid();
            var nowUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "Best face selected for enrollment. UserId={UserId}, FaceId={FaceId}, OverallScore={Score:F3}, Width={Width}, Height={Height}, Dim={Dim}",
                userId,
                bestFace.FaceId,
                bestFace.Quality?.OverallScore ?? 0f,
                bestFace.Bbox?.W ?? 0f,
                bestFace.Bbox?.H ?? 0f,
                embedding.Count);

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
                embedding.Count,
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
