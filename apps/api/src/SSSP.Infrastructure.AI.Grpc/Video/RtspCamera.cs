using OpenCvSharp;
using System;

namespace SSSP.Infrastructure.AI.Grpc.Video
{
    public sealed class RtspCamera : IDisposable
    {
        private readonly VideoCapture _cap;

        public string CameraId { get; }
        public string RtspUrl { get; }

        public RtspCamera(string cameraId, string rtspUrl)
        {
            CameraId = cameraId;
            RtspUrl = rtspUrl;

            _cap = new VideoCapture(rtspUrl);
            if (!_cap.IsOpened())
            {
                throw new InvalidOperationException(
                    $"RTSP stream not accessible for camera {cameraId} ({rtspUrl})");
            }
        }

        public byte[]? ReadJpeg()
        {
            using var frame = new Mat();
            if (!_cap.Read(frame) || frame.Empty())
                return null;

            return frame.ToBytes(".jpg");
        }

        public void Dispose()
        {
            _cap?.Release();
        }
    }
}
