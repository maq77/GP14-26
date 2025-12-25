namespace SSSP.BL.DTOs
{
    public sealed class CameraEdgeDTO
    {
        public string FromCameraId { get; set; } = default!;
        public string ToCameraId { get; set; } = default!;
        public double TravelSeconds { get; set; }
    }
}
