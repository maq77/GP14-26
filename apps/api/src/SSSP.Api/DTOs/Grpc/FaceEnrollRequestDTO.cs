using System;
using Microsoft.AspNetCore.Http;

namespace SSSP.Api.DTOs.Grpc
{
    public class FaceEnrollRequestDto
    {
        public Guid UserId { get; set; }
        public string? Description { get; set; }
        public IFormFile Image { get; set; } = null!;
    }
}
