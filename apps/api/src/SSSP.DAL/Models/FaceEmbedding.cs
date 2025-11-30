using SSSP.DAL.Abstractions;

namespace SSSP.DAL.Models
{
    public class FaceEmbedding : IEntity<int>
    {
        public int Id { get; set; }

        // Navugation to FaceProfile
        public Guid FaceProfileId { get; set; }
        public FaceProfile FaceProfile { get; set; } = null!;

        public byte[] Vector { get; set; } = Array.Empty<byte>();

        public string? SourceCameraId { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
