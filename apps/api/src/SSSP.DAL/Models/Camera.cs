using SSSP.DAL.Abstractions;
using SSSP.DAL.Enums;
using SSSP.DAL.ValueObjects;

namespace SSSP.DAL.Models;

public class Camera : IEntity<int>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public string RtspUrl { get; set; } = string.Empty;
    public Location? Location { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastSeenAt { get; set; } = DateTime.Now;
    public string ZoneId { get; set; } = "Default-Zone";
    public CameraAICapabilities Capabilities { get; set; } = CameraAICapabilities.Face;

    public CameraRecognitionMode RecognitionMode { get; set; } = CameraRecognitionMode.Normal;
    public double? MatchThresholdOverride { get; set; } = 0.6;
}
