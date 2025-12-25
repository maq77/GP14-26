using System;
using Microsoft.AspNetCore.Http;
using SSSP.DAL.Enums;

namespace SSSP.Api.DTOs.Camera
{
    public sealed class CreateCameraRequest
    {
        public string Name { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public CameraAICapabilities Capabilities { get; set; } = CameraAICapabilities.Face;
        public CameraRecognitionMode RecognitionMode { get; set; } = CameraRecognitionMode.Normal;
        public double? MatchThresholdOverride { get; set; }
        public string? ZoneId { get; set; } = "Default-Zone";
    }
}
