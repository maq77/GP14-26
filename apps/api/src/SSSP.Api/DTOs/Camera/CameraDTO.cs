using System;
using Microsoft.AspNetCore.Http;
using SSSP.DAL.Enums;

namespace SSSP.Api.DTOs.Camera
{
    public sealed class CameraDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public CameraAICapabilities Capabilities { get; set; }
        public CameraRecognitionMode RecognitionMode { get; set; }
        public double? MatchThresholdOverride { get; set; }
    }
}
