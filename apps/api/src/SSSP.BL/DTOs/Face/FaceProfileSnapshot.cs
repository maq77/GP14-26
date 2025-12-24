using System;
using System.Collections.Generic;

namespace SSSP.BL.DTOs.Faces
{
    public sealed class FaceProfileSnapshot
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }

        public string UserName { get; init; } = "N/A";
        public string FullName { get; init; } = "Name Unassigned";

        public bool IsPrimary { get; init; }
        public DateTime CreatedAt { get; init; }

        public IReadOnlyList<FaceEmbeddingSnapshot> Embeddings { get; init; }
            = Array.Empty<FaceEmbeddingSnapshot>();
    }
}
