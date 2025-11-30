using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Managers;
using SSSP.BL.Managers.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;
using SSSP.BL.Records;
using SSSP.BL.Services.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly IAIFaceClient _ai;
        private readonly IFaceMatchingManager _matcher;
        private readonly IFaceProfileCache _faceProfileCache;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceRecognitionService> _logger;

        public FaceRecognitionService(
            IAIFaceClient ai,
            IFaceMatchingManager matcher,
            IFaceProfileCache faceProfileCache,
            IUnitOfWork uow,
            ILogger<FaceRecognitionService> logger)
        {
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _faceProfileCache = faceProfileCache ?? throw new ArgumentNullException(nameof(faceProfileCache));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FaceMatchResult> VerifyAsync(
            byte[] image,
            string cameraId,
            CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
            {
                _logger.LogWarning(
                    "VerifyAsync called with empty image. Camera={CameraId}",
                    cameraId ?? "N/A");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CameraId"] = cameraId ?? "N/A",
                ["Mode"] = "SingleImage"
            });

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Face verify started. Camera={CameraId} ImageSize={Size}",
                    cameraId,
                    image.Length);

                var embeddingResult = await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

                if (embeddingResult == null ||
                    embeddingResult.Embedding == null ||
                    embeddingResult.Embedding.Count == 0 ||
                    !embeddingResult.FaceDetected)
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "VerifyAsync failed. No valid embedding from AI. Camera={CameraId} FaceDetected={Detected} ElapsedMs={Elapsed}",
                        cameraId,
                        embeddingResult?.FaceDetected ?? false,
                        sw.ElapsedMilliseconds);

                    return new FaceMatchResult(false, null, null, 0.0);
                }

                var result = await MatchInternalAsync(
                    embeddingResult.Embedding,
                    cameraId,
                    ct);

                sw.Stop();

                _logger.LogInformation(
                    "VerifyAsync completed. Camera={CameraId} IsMatch={IsMatch} Similarity={Similarity} ElapsedMs={Elapsed}",
                    cameraId,
                    result.IsMatch,
                    result.Similarity,
                    sw.ElapsedMilliseconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning(
                    "VerifyAsync canceled. Camera={CameraId} ElapsedMs={Elapsed}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "VerifyAsync failed with exception. Camera={CameraId} ElapsedMs={Elapsed}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task<FaceMatchResult> VerifyEmbeddingAsync(
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct = default)
        {
            if (embedding == null || embedding.Count == 0)
            {
                _logger.LogDebug(
                    "VerifyEmbeddingAsync skipped. Empty embedding. Camera={CameraId}",
                    cameraId ?? "N/A");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CameraId"] = cameraId ?? "N/A",
                ["Mode"] = "Streaming"
            });

            var sw = Stopwatch.StartNew();

            try
            {
                var result = await MatchInternalAsync(embedding, cameraId, ct);

                sw.Stop();

                _logger.LogDebug(
                    "VerifyEmbeddingAsync completed. Camera={CameraId} IsMatch={IsMatch} Similarity={Similarity} ElapsedMs={Elapsed}",
                    cameraId,
                    result.IsMatch,
                    result.Similarity,
                    sw.ElapsedMilliseconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogDebug(
                    "VerifyEmbeddingAsync canceled. Camera={CameraId} ElapsedMs={Elapsed}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "VerifyEmbeddingAsync failed with exception. Camera={CameraId} ElapsedMs={Elapsed}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task<FaceMatchResult> MatchInternalAsync(
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct)
        {
            var policy = await ResolveCameraPolicyAsync(cameraId, ct);

            if (policy.Mode == CameraRecognitionMode.Disabled)
            {
                _logger.LogInformation(
                    "Recognition disabled for Camera={CameraId}. Skipping match.",
                    policy.CameraId);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var profiles = await _faceProfileCache.GetAllAsync(ct);

            if (profiles.Count == 0)
            {
                _logger.LogInformation(
                    "No FaceProfiles found for matching. Camera={CameraId}",
                    policy.CameraId);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var result = _matcher.Match(
                embedding,
                profiles,
                policy.EffectiveThreshold);

            if (policy.Mode == CameraRecognitionMode.ObserveOnly)
            {
                _logger.LogInformation(
                    "ObserveOnly mode. Camera={CameraId} Similarity={Similarity} Threshold={Threshold}",
                    policy.CameraId,
                    result.Similarity,
                    policy.EffectiveThreshold);

                return new FaceMatchResult(false, null, null, result.Similarity);
            }

            if (result.IsMatch)
            {
                var confidenceBucket = BucketizeConfidence(result.Similarity);

                _logger.LogInformation(
                    "Face match success. Camera={CameraId} User={UserId} FaceProfile={FaceProfileId} Similarity={Similarity} Threshold={Threshold} Mode={Mode} Confidence={ConfidenceBucket}",
                    policy.CameraId,
                    result.UserId,
                    result.FaceProfileId,
                    result.Similarity,
                    policy.EffectiveThreshold,
                    policy.Mode,
                    confidenceBucket);
            }
            else
            {
                _logger.LogDebug(
                    "Face match failed. Camera={CameraId} BestSimilarity={Similarity} Threshold={Threshold} Mode={Mode}",
                    policy.CameraId,
                    result.Similarity,
                    policy.EffectiveThreshold,
                    policy.Mode);
            }

            return result;
        }

        private async Task<CameraRecognitionPolicy> ResolveCameraPolicyAsync(
    string cameraId,
    CancellationToken ct)
        {
            var mode = CameraRecognitionMode.Normal;
            var threshold = _matcher.DefaultThreshold;
            var resolvedCameraId = cameraId ?? "N/A";

            if (int.TryParse(cameraId, out var id))
            {
                var repo = _uow.GetRepository<Camera, int>();
                var camera = await repo.GetByIdAsync(id, ct);

                if (camera != null)
                {
                    resolvedCameraId = camera.Id.ToString();
                    mode = camera.RecognitionMode;

                    if (camera.MatchThresholdOverride.HasValue)
                        threshold = camera.MatchThresholdOverride.Value;

                    // NEW: if this camera is not configured for face AI, treat it as disabled for face recognition
                    if (!camera.Capabilities.HasFlag(CameraAICapabilities.Face))
                        mode = CameraRecognitionMode.Disabled;
                }
            }

            threshold = mode switch
            {
                CameraRecognitionMode.Strict => Math.Min(1.0, threshold + 0.05),
                CameraRecognitionMode.Relaxed => Math.Max(0.0, threshold - 0.05),
                _ => threshold
            };

            return new CameraRecognitionPolicy(
                resolvedCameraId,
                mode,
                threshold);
        }

        private static string BucketizeConfidence(double similarity)
        {
            if (similarity >= 0.85) return "High";
            if (similarity >= 0.65) return "Medium";
            if (similarity > 0.0) return "Low";
            return "None";
        }
    }
}
