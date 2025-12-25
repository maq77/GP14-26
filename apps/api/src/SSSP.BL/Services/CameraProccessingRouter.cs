//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Channels;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using Sssp.Ai.Face;
//using SSSP.Infrastructure.AI.Grpc.Interfaces;

//namespace SSSP.BL.Services
//{
//    /// <summary>
//    /// Routes camera frames to appropriate AI clients based on camera mode.
//    /// Enables parallel processing for multi-mode cameras.
//    /// </summary>
//    public sealed class CameraProcessingRouter : IDisposable
//    {
//        private readonly IAIFaceClient _faceClient;
//        private readonly ILogger<CameraProcessingRouter> _logger;

//        // Frame queue with backpressure
//        private readonly Channel<CameraFrame> _frameQueue;
//        private readonly SemaphoreSlim _processingThrottle;

//        private const int MAX_QUEUE_DEPTH = 30; // ~1 second at 30fps
//        private const int MAX_PARALLEL_FRAMES = 4;

//        public CameraProcessingRouter(
//            IAIFaceClient faceClient,
//            ILogger<CameraProcessingRouter> logger)
//        {
//            _faceClient = faceClient;
//            _logger = logger;

//            _frameQueue = Channel.CreateBounded<CameraFrame>(new BoundedChannelOptions(MAX_QUEUE_DEPTH)
//            {
//                FullMode = BoundedChannelFullMode.DropOldest // Drop old frames under load
//            });

//            _processingThrottle = new SemaphoreSlim(MAX_PARALLEL_FRAMES);
//        }

//        public async Task<CameraProcessingResult> ProcessFrameAsync(
//            CameraFrame frame,
//            CameraMode mode,
//            CancellationToken cancellationToken = default)
//        {
//            var startTime = DateTimeOffset.UtcNow;

//            try
//            {
//                // Route based on camera mode
//                var result = mode switch
//                {
//                    CameraMode.FaceOnly => await ProcessFaceOnlyAsync(frame, cancellationToken),
//                    CameraMode.FaceAndObject => await ProcessMultiModalAsync(frame, cancellationToken),
//                    CameraMode.Full => await ProcessFullPipelineAsync(frame, cancellationToken),
//                    _ => throw new ArgumentException($"Unknown camera mode: {mode}")
//                };

//                var elapsed = DateTimeOffset.UtcNow - startTime;
//                result.ProcessingTimeMs = elapsed.TotalMilliseconds;

//                return result;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex,
//                    "Frame processing failed. CameraId={CameraId}, Mode={Mode}, FrameId={FrameId}",
//                    frame.CameraId, mode, frame.FrameId);

//                return CameraProcessingResult.Failed(frame.CameraId, frame.FrameId, ex.Message);
//            }
//        }

//        /// <summary>
//        /// Fast path: Direct face detection + embedding via FaceClient
//        /// Bypasses video streaming overhead
//        /// </summary>
//        private async Task<CameraProcessingResult> ProcessFaceOnlyAsync(
//            CameraFrame frame,
//            CancellationToken cancellationToken)
//        {
//            await _processingThrottle.WaitAsync(cancellationToken);

//            try
//            {
//                // Use the dedicated face RPC (not video stream)
//                var request = new FaceEmbeddingRequest
//                {
//                    Image = Google.Protobuf.ByteString.CopyFrom(frame.JpegData),
//                    CameraId = frame.CameraId.ToString(),
//                    ConfidenceThreshold = 0.7f,
//                    MaxFaces = 10,
//                    IncludeCrops = false, // Don't need crops for matching
//                    MaxImageDimension = 1280
//                };

//                var response = await _faceClient.ExtractEmbeddingsAsync(request, cancellationToken);

//                if (!response.Success || !response.FaceDetected)
//                {
//                    return CameraProcessingResult.NoFaces(frame.CameraId, frame.FrameId);
//                }

//                var faces = response.Faces
//                    .Select(f => new DetectedFaceData
//                    {
//                        BoundingBox = new BBox(f.Bbox.X, f.Bbox.Y, f.Bbox.W, f.Bbox.H),
//                        Confidence = f.Confidence,
//                        Embedding = f.EmbeddingVector.ToArray(),
//                        Quality = new FaceQualityData
//                        {
//                            OverallScore = f.Quality.OverallScore,
//                            Sharpness = f.Quality.Sharpness,
//                            Brightness = f.Quality.Brightness,
//                            FaceSizePixels = f.Quality.FaceSizePixels
//                        }
//                    })
//                    .ToList();

//                return CameraProcessingResult.Success(
//                    frame.CameraId,
//                    frame.FrameId,
//                    faces,
//                    response.TotalTimeMs);
//            }
//            finally
//            {
//                _processingThrottle.Release();
//            }
//        }

//        /// <summary>
//        /// Parallel processing: Face + Object detection simultaneously
//        /// </summary>
//        private async Task<CameraProcessingResult> ProcessMultiModalAsync(
//            CameraFrame frame,
//            CancellationToken cancellationToken)
//        {
//            await _processingThrottle.WaitAsync(cancellationToken);

//            try
//            {
//                // TODO: Add object detection client
//                // For now, just face processing
//                var faceTask = ProcessFaceOnlyAsync(frame, cancellationToken);

//                // Future: var objectTask = _objectClient.DetectObjectsAsync(frame, cancellationToken);
//                // await Task.WhenAll(faceTask, objectTask);

//                var result = await faceTask;

//                _logger.LogDebug(
//                    "Multi-modal processing. CameraId={CameraId}, Faces={FaceCount}, TimeMs={TimeMs}",
//                    frame.CameraId, result.Faces.Count, result.ProcessingTimeMs);

//                return result;
//            }
//            finally
//            {
//                _processingThrottle.Release();
//            }
//        }

//        /// <summary>
//        /// Full pipeline: Face + Object + Behavior + Tracking
//        /// </summary>
//        private async Task<CameraProcessingResult> ProcessFullPipelineAsync(
//            CameraFrame frame,
//            CancellationToken cancellationToken)
//        {
//            // TODO: Implement when behavior analysis is ready
//            return await ProcessMultiModalAsync(frame, cancellationToken);
//        }

//        public void Dispose()
//        {
//            _frameQueue.Writer.Complete();
//            _processingThrottle?.Dispose();
//        }
//    }

//    // Supporting types
//    public enum CameraMode
//    {
//        FaceOnly,       // Gate cameras - only face recognition
//        FaceAndObject,  // Office cameras - faces + objects
//        Full            // Security cameras - all features
//    }

//    public sealed record CameraFrame(
//        int CameraId,
//        long FrameId,
//        byte[] JpegData,
//        long TimestampMs);

//    public sealed class CameraProcessingResult
//    {
//        public int CameraId { get; init; }
//        public long FrameId { get; init; }
//        public bool Success { get; init; }
//        public string? ErrorMessage { get; init; }
//        public List<DetectedFaceData> Faces { get; init; } = new();
//        public double ProcessingTimeMs { get; set; }

//        public static CameraProcessingResult Success(
//            int cameraId,
//            long frameId,
//            List<DetectedFaceData> faces,
//            double processingTimeMs = 0) => new()
//            {
//                CameraId = cameraId,
//                FrameId = frameId,
//                Success = true,
//                Faces = faces,
//                ProcessingTimeMs = processingTimeMs
//            };

//        public static CameraProcessingResult NoFaces(int cameraId, long frameId) => new()
//        {
//            CameraId = cameraId,
//            FrameId = frameId,
//            Success = true,
//            Faces = new List<DetectedFaceData>()
//        };

//        public static CameraProcessingResult Failed(
//            int cameraId,
//            long frameId,
//            string error) => new()
//            {
//                CameraId = cameraId,
//                FrameId = frameId,
//                Success = false,
//                ErrorMessage = error
//            };
//    }

//    public sealed class DetectedFaceData
//    {
//        public BBox BoundingBox { get; init; }
//        public float Confidence { get; init; }
//        public float[] Embedding { get; init; } = Array.Empty<float>();
//        public FaceQualityData Quality { get; init; }
//    }

//    public sealed record BBox(float X, float Y, float W, float H);

//    public sealed class FaceQualityData
//    {
//        public float OverallScore { get; init; }
//        public float Sharpness { get; init; }
//        public float Brightness { get; init; }
//        public int FaceSizePixels { get; init; }
//    }
//}