using SSSP.BL.Records;
using System;

namespace SSSP.Api.DTOs.Face
{
    public sealed class MultiFaceMatchResponse
    {
        public int? FaceId { get; set; }

        public FaceBoundingBox BoundingBox { get; set; } = default!;

        public float OverallQuality { get; set; }

        public bool IsMatch { get; set; }

        public Guid? UserId { get; set; }

        public Guid? FaceProfileId { get; set; }

        public double Similarity { get; set; }
    }
}
