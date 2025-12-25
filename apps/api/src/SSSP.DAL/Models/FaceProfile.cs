using SSSP.DAL.Abstractions;
using System;

namespace SSSP.DAL.Models
{
    public class FaceProfile : IEntity<Guid>
    {
        public Guid Id { get; set; }

        // Link to User (FK)
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // Serialized embedding (e.g. JSON array of floats) old code
        //public string EmbeddingJson { get; set; } = string.Empty;
        //new code
        public ICollection<FaceEmbedding> Embeddings { get; set; } = new List<FaceEmbedding>();

        // Optional metadata
        public bool IsPrimary { get; set; } = true;
        public string? Description { get; set; } // "Front-facing", "Gate 1", etc.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
