using System;
using System.Collections.Generic;

namespace SSSP.BL.DTOs.Faces
{
    public sealed class FaceEmbeddingSnapshot
    {
        public int Id { get; init; }
        public float[] Vector { get; init; } = Array.Empty<float>();
    }
}
