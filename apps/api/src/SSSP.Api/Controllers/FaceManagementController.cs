using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Face;
using SSSP.BL.Interfaces;
using SSSP.BL.Services.Interfaces;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Roles = "Admin,Operator")]
    public class FaceManagementController : ControllerBase
    {
        private readonly IFaceManagementService _faceManagementService;
        private readonly ILogger<FaceManagementController> _logger;

        public FaceManagementController(
            IFaceManagementService faceManagementService,
            ILogger<FaceManagementController> logger)
        {
            _faceManagementService = faceManagementService;
            _logger = logger;
        }

        [HttpGet("user/{userId:guid}/profiles")]
        public async Task<IActionResult> GetProfilesForUser(
            Guid userId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP get face profiles requested for user {UserId}",
                userId);

            var profiles = await _faceManagementService.GetProfilesForUserAsync(userId, ct);

            var dto = profiles.Select(p => new FaceProfileDTO
            {
                Id = p.Id,
                UserId = p.UserId,
                Description = p.Description,
                IsPrimary = p.IsPrimary,
                CreatedAt = p.CreatedAt
            });

            return Ok(dto);
        }

        [HttpGet("profiles/{profileId:guid}")]
        public async Task<IActionResult> GetProfile(
            Guid profileId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP get face profile requested. ProfileId={ProfileId}",
                profileId);

            var profile = await _faceManagementService.GetProfileAsync(profileId, ct);

            if (profile == null)
                return NotFound("Face profile not found");

            var dto = new FaceProfileDTO
            {
                Id = profile.Id,
                UserId = profile.UserId,
                Description = profile.Description,
                IsPrimary = profile.IsPrimary,
                CreatedAt = profile.CreatedAt
            };

            return Ok(dto);
        }

        [HttpDelete("profiles/{profileId:guid}")]
        public async Task<IActionResult> DeleteProfile(
            Guid profileId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP delete face profile requested. ProfileId={ProfileId}",
                profileId);

            var deleted = await _faceManagementService.DeleteProfileAsync(profileId, ct);

            if (!deleted)
                return NotFound("Face profile not found");

            return NoContent();
        }

        [HttpPost("profiles/{profileId:guid}/set-primary")]
        public async Task<IActionResult> SetPrimary(
            Guid profileId,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "HTTP set-primary requested for face profile {ProfileId}",
                profileId);

            var updated = await _faceManagementService.SetPrimaryProfileAsync(profileId, ct);

            if (!updated)
                return NotFound("Face profile not found");

            return NoContent();
        }
    }
}
