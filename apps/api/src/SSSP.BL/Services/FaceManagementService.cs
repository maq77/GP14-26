// SSSP.BL.Services/FaceManagementService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceManagementService : IFaceManagementService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceManagementService> _logger;

        public FaceManagementService(
            IUnitOfWork uow,
            ILogger<FaceManagementService> logger)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<FaceProfile>> GetProfilesForUserAsync(
            Guid userId,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var all = await repo.GetAllAsync(ct);

            var profiles = all
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.IsPrimary)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();

            _logger.LogDebug(
                "Loaded {Count} face profiles for user {UserId}",
                profiles.Count,
                userId);

            return profiles;
        }

        public async Task<FaceProfile?> GetProfileAsync(
            Guid profileId,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var profile = await repo.GetByIdAsync(profileId, ct);

            if (profile == null)
            {
                _logger.LogWarning(
                    "Face profile {ProfileId} not found",
                    profileId);
            }

            return profile;
        }

        public async Task<bool> DeleteProfileAsync(
            Guid profileId,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var profile = await repo.GetByIdAsync(profileId, ct);

            if (profile == null)
            {
                _logger.LogWarning(
                    "Face profile {ProfileId} not found for delete",
                    profileId);
                return false;
            }

            await repo.DeleteAsync(profileId, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Face profile deleted. ProfileId={ProfileId} UserId={UserId}",
                profile.Id,
                profile.UserId);

            return true;
        }

        public async Task<bool> SetPrimaryProfileAsync(
            Guid profileId,
            CancellationToken ct)
        {
            var repo = _uow.GetRepository<FaceProfile, Guid>();
            var target = await repo.GetByIdAsync(profileId, ct);

            if (target == null)
            {
                _logger.LogWarning(
                    "Face profile {ProfileId} not found for set-primary",
                    profileId);
                return false;
            }

            var all = await repo.GetAllAsync(ct);

            foreach (var profile in all.Where(p => p.UserId == target.UserId))
            {
                profile.IsPrimary = profile.Id == target.Id;
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Face profile set as primary. ProfileId={ProfileId} UserId={UserId}",
                target.Id,
                target.UserId);

            return true;
        }
    }
}
