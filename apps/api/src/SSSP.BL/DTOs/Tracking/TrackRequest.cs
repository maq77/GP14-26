namespace SSSP.BL.DTOs.Tracking
{
    internal class TrackRequest
    {
        public string CameraId { get; set; }
        public Guid FaceId { get; set; }
        public double Similarity { get; set; }
        public DateTime Timestamp { get; set; }
    }
}