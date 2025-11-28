using System;

namespace SSSP.Api.DTOs.Face
{
    public class FaceMatchResponse
    {
        public bool IsMatch { get; set; }
        public Guid? UserId { get; set; }
        public Guid? FaceProfileId { get; set; }
        public double Similarity { get; set; }
    }
}
