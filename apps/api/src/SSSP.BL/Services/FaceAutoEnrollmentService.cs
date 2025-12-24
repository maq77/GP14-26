using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.BL.Interfaces;
using SSSP.BL.Options;
using SSSP.BL.Services.Interfaces;
using SSSP.BL.Utils;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceAutoEnrollmentService : IFaceAutoEnrollmentService
    {
        // enterprise safety rails
        /*private const double AUTO_ENROLL_MIN_SIMILARITY = 0.92;
        private const double MIN_VARIATION_DISTANCE = 0.08;          // 1 - cosine
        private const int MAX_EMBEDDINGS_PER_PROFILE = 10;
        private static readonly TimeSpan MIN_INTERVAL_BETWEEN_ENROLL = TimeSpan.FromMinutes(10);*/

        private readonly IUnitOfWork _uow;
        private readonly IFaceProfileCache _faceProfileCache;
        private readonly ILogger<FaceAutoEnrollmentService> _logger;
        private readonly FaceRecognitionOptions _options;

        // per-user throttling
        private readonly ConcurrentDictionary<Guid, DateTime> _lastEnrollmentByUser = new();

        public FaceAutoEnrollmentService(
            IUnitOfWork uow,
            IFaceProfileCache faceProfileCache,
            ILogger<FaceAutoEnrollmentService> logger,
            IOptions<FaceRecognitionOptions> options)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _faceProfileCache = faceProfileCache ?? throw new ArgumentNullException(nameof(faceProfileCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task TryAutoEnrollAsync(
            Guid userId,
            Guid faceProfileId,
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct)
        {
            if (embedding == null || embedding.Count == 0)
                return;

            var nowUtc = DateTime.UtcNow;

            if (_lastEnrollmentByUser.TryGetValue(userId, out var last) &&
                nowUtc - last < _options.AutoEnrollment.MinIntervalBetweenEnroll)
            {
                _logger.LogDebug(
                    "Auto-enroll skipped due to rate limit. UserId={UserId}, Last={Last:o}, Now={Now:o}",
                    userId, last, nowUtc);
                return;
            }

            var repo = _uow.GetRepository<FaceProfile, Guid>();

            var profile = await repo.Query
                .Include(p => p.Embeddings)
                .FirstOrDefaultAsync(p => p.Id == faceProfileId, ct);

            if (profile == null)
            {
                _logger.LogWarning(
                    "Auto-enroll skipped. FaceProfile not found. UserId={UserId}, FaceProfileId={FaceProfileId}",
                    userId, faceProfileId);
                return;
            }

            if (profile.Embeddings != null &&
                profile.Embeddings.Count >= _options.AutoEnrollment.MaxEmbeddingsPerProfile)
            {
                _logger.LogDebug(
                    "Auto-enroll skipped. Max embeddings reached. UserId={UserId}, FaceProfileId={FaceProfileId}, Count={Count}",
                    userId, faceProfileId, profile.Embeddings.Count);
                return;
            }

            if (profile.Embeddings != null &&
                !IsEmbeddingSufficientlyDifferent(profile.Embeddings, embedding))
            {
                _logger.LogDebug(
                    "Auto-enroll skipped. New embedding too similar to existing ones. UserId={UserId}, FaceProfileId={FaceProfileId}",
                    userId, faceProfileId);
                return;
            }

            var vectorBytes = EmbeddingMath.ToByteArray(embedding);

            profile.Embeddings ??= new List<FaceEmbedding>();
            profile.Embeddings.Add(new FaceEmbedding
            {
                FaceProfileId = profile.Id,
                Vector = vectorBytes,
                SourceCameraId = cameraId,
                CreatedAt = nowUtc
            });

            await _uow.SaveChangesAsync(ct);
            await _faceProfileCache.InvalidateAsync();

            _lastEnrollmentByUser[userId] = nowUtc;

            _logger.LogInformation(
                "AUTO-ENROLL COMPLETED. UserId={UserId}, FaceProfileId={FaceProfileId}, NewEmbeddingDim={Dim}, TotalEmbeddings={Count}",
                userId,
                faceProfileId,
                embedding.Count,
                profile.Embeddings?.Count ?? 0);
        }

        private  bool IsEmbeddingSufficientlyDifferent(
            IEnumerable<FaceEmbedding> existingEmbeddings,
            IReadOnlyList<float> newEmbedding)
        {
            // no embeddings yet -> always considered "different enough"
            if (existingEmbeddings == null)
                return true;

            foreach (var emb in existingEmbeddings)
            {
                if (emb?.Vector == null || emb.Vector.Length == 0)
                    continue;

                var storedFloats = EmbeddingMath.ByteArrayToFloatArray(emb.Vector);

                if (storedFloats.Length != newEmbedding.Count)
                    continue;

                var similarity = EmbeddingMath.ComputeCosineSimilarity(newEmbedding, storedFloats);
                var distance = 1.0 - similarity;

                if (distance < _options.AutoEnrollment.MinVariationDistance)
                {
                    // too close to existing embedding, no new information
                    return false;
                }
            }

            return true;
        }
    }
}
