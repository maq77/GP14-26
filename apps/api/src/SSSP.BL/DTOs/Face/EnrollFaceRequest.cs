using System;
using Microsoft.AspNetCore.Http;

namespace SSSP.Api.DTOs.Face
{
    public class EnrollFaceRequest
    {
        public Guid UserId { get; set; }
        public IFormFile Image { get; set; } = default!;
        public string? Description { get; set; }
    }
}
