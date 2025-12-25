using System;

namespace SSSP.Api.DTOs.Face
{
    public sealed class FaceProfileDTO
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? Description { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
