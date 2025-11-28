using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Managers;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public class FaceEnrollmentService
    {
        private readonly IAIFaceClient _ai;
        private readonly IUnitOfWork _uow;
        private readonly FaceMatchingManager _matcher;
        private readonly ILogger<FaceEnrollmentService> _logger;

        public FaceEnrollmentService(
            IAIFaceClient ai,
            IUnitOfWork uow,
            FaceMatchingManager matcher,
            ILogger<FaceEnrollmentService> logger)
        {
            _ai = ai;
            _uow = uow;
            _matcher = matcher;
            _logger = logger;
        }

        public async Task<FaceProfile> EnrollAsync(
            Guid userId,
            byte[] image,
            string? description,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Face enrollment started for user {UserId}. Image size {Length} bytes",
                userId,
                image?.Length ?? 0);

            var embeddingResult = await _ai.ExtractEmbeddingAsync(image, string.Empty, ct);

            if (embeddingResult.Embedding == null || embeddingResult.Embedding.Count == 0)
            {
                _logger.LogWarning(
                    "AI returned empty embedding during enrollment for user {UserId}",
                    userId);
                throw new InvalidOperationException("No embedding returned from AI");
            }

            _logger.LogInformation(
                "AI embedding extracted for enrollment. User {UserId} Dimension {Dim}",
                userId,
                embeddingResult.Embedding.Count);

            var userRepo = _uow.GetRepository<User, Guid>();
            var user = await userRepo.GetByIdAsync(userId, ct);
            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found during face enrollment",
                    userId);
                throw new KeyNotFoundException("User not found");
            }

            var embeddingJson = JsonSerializer.Serialize(embeddingResult.Embedding);

            var profile = new FaceProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmbeddingJson = embeddingJson,
                IsPrimary = true,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            var profileRepo = _uow.GetRepository<FaceProfile, Guid>();
            await profileRepo.AddAsync(profile, ct);

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Face profile {FaceProfileId} enrolled successfully for user {UserId}",
                profile.Id,
                userId);

            return profile;
        }
    }
}
