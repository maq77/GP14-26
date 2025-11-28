using System;
using Microsoft.AspNetCore.Http;

namespace SSSP.Api.DTOs.Grpc
{
    public class StartCameraRequest
    {
        public string? RtspUrl { get; set; } = string.Empty;
    }

}
