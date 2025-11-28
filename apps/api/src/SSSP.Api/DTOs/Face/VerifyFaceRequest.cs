using Microsoft.AspNetCore.Http;

namespace SSSP.Api.DTOs.Face
{
    public class VerifyFaceRequest
    {
        public string CameraId { get; set; } = string.Empty;
        public IFormFile Image { get; set; } = default!;
    }
}
