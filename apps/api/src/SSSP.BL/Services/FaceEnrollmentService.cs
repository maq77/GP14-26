using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceEnrollmentService
    {
        private readonly IAIFaceClient _ai;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceEnrollmentService> _logger;

        public FaceEnrollmentService(
            IAIFaceClient ai,
            IUnitOfWork uow,
            ILogger<FaceEnrollmentService> logger)
        {
            _ai = ai;
            _uow = uow;
            _logger = logger;
        }

        public async Task<FaceProfile> EnrollAsync(
            Guid userId,
            byte[] image,
            string? description,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Face enrollment started. UserId={UserId} ImageSize={Size}",
                userId,
                image?.Length ?? 0);

            var embeddingResult =
                await _ai.ExtractEmbeddingAsync(image, string.Empty, ct);

            if (embeddingResult.Embedding == null ||
                embeddingResult.Embedding.Count == 0)
            {
                _logger.LogWarning(
                    "Enrollment failed. Empty embedding from AI. UserId={UserId}",
                    userId);
                throw new InvalidOperationException("Empty embedding returned from AI");
            }

            var profile = new FaceProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EmbeddingJson =
                    JsonSerializer.Serialize(embeddingResult.Embedding),
                IsPrimary = true,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            var repo = _uow.GetRepository<FaceProfile, Guid>();
            await repo.AddAsync(profile, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Face enrollment completed. UserId={UserId} ProfileId={ProfileId} Dim={Dim}",
                userId,
                profile.Id,
                embeddingResult.Embedding.Count);

            return profile;
        }
    }
}
