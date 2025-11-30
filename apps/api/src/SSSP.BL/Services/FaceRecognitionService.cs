using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SSSP.BL.Interfaces;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.Records;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public sealed class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly IAIFaceClient _ai;
        private readonly IFaceMatchingManager _matcher;
        private readonly IFaceProfileCache _faceProfileCache;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceRecognitionService> _logger;

        private const int MIN_EMBEDDING_SIZE = 128;
        private const double HIGH_CONFIDENCE = 0.85;
        private const double MEDIUM_CONFIDENCE = 0.65;

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
                _logger.LogWarning("Verify called with empty image. CameraId={CameraId}", cameraId ?? "N/A");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Face verification started. CameraId={CameraId}, ImageSize={ImageSize}, Mode=SingleImage",
                    cameraId, image.Length);

                var embeddingResult = await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

                if (embeddingResult == null ||
                    embeddingResult.Embedding == null ||
                    embeddingResult.Embedding.Count == 0 ||
                    !embeddingResult.FaceDetected)
                {
                    sw.Stop();

                    _logger.LogWarning(
                        "Face extraction failed. CameraId={CameraId}, FaceDetected={FaceDetected}, EmbeddingSize={EmbeddingSize}, ElapsedMs={ElapsedMs}",
                        cameraId,
                        embeddingResult?.FaceDetected ?? false,
                        embeddingResult?.Embedding?.Count ?? 0,
                        sw.ElapsedMilliseconds);

                    return new FaceMatchResult(false, null, null, 0.0);
                }

                var result = await MatchInternalAsync(embeddingResult.Embedding, cameraId, ct);

                sw.Stop();

                _logger.LogInformation(
                    "Face verification completed. CameraId={CameraId}, Mode=SingleImage, IsMatch={IsMatch}, Similarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                    cameraId, result.IsMatch, result.Similarity, sw.ElapsedMilliseconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning("Face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Face verification failed. CameraId={CameraId}, ExceptionType={ExceptionType}, ElapsedMs={ElapsedMs}",
                    cameraId, ex.GetType().Name, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task<FaceMatchResult> VerifyEmbeddingAsync(
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct = default)
        {
            if (embedding == null || embedding.Count < MIN_EMBEDDING_SIZE)
            {
                _logger.LogDebug("Invalid embedding. CameraId={CameraId}, EmbeddingSize={EmbeddingSize}",
                    cameraId ?? "N/A", embedding?.Count ?? 0);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                var result = await MatchInternalAsync(embedding, cameraId, ct);

                sw.Stop();

                if (result.IsMatch)
                {
                    _logger.LogInformation(
                        "Face verification completed. CameraId={CameraId}, Mode=Streaming, IsMatch={IsMatch}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                        cameraId, result.IsMatch, result.UserId, result.FaceProfileId, result.Similarity, sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogDebug(
                        "Face verification completed. CameraId={CameraId}, Mode=Streaming, IsMatch={IsMatch}, BestSimilarity={Similarity:F4}, ElapsedMs={ElapsedMs}",
                        cameraId, result.IsMatch, result.Similarity, sw.ElapsedMilliseconds);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogDebug("Embedding verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId, sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Embedding verification failed. CameraId={CameraId}, ExceptionType={ExceptionType}, ElapsedMs={ElapsedMs}",
                    cameraId, ex.GetType().Name, sw.ElapsedMilliseconds);
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
                _logger.LogDebug("Recognition disabled. CameraId={CameraId}, Mode={Mode}",
                    policy.CameraId, policy.Mode);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var profiles = await _faceProfileCache.GetAllAsync(ct);

            if (profiles.Count == 0)
            {
                _logger.LogWarning("No face profiles available for matching. CameraId={CameraId}",
                    policy.CameraId);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var result = _matcher.Match(embedding, profiles, policy.EffectiveThreshold);

            if (policy.Mode == CameraRecognitionMode.ObserveOnly)
            {
                _logger.LogInformation(
                    "Observe-only mode. CameraId={CameraId}, Mode={Mode}, Similarity={Similarity:F4}, Threshold={Threshold:F4}, Profiles={ProfileCount}",
                    policy.CameraId, policy.Mode, result.Similarity, policy.EffectiveThreshold, profiles.Count);

                return new FaceMatchResult(false, null, null, result.Similarity);
            }

            if (result.IsMatch)
            {
                var confidence = BucketizeConfidence(result.Similarity);

                _logger.LogInformation(
                    "FACE MATCHED. CameraId={CameraId}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, Threshold={Threshold:F4}, Confidence={Confidence}, Mode={Mode}, Profiles={ProfileCount}",
                    policy.CameraId, result.UserId, result.FaceProfileId, result.Similarity,
                    policy.EffectiveThreshold, confidence, policy.Mode, profiles.Count);
            }
            else
            {
                _logger.LogDebug(
                    "No face match. CameraId={CameraId}, BestSimilarity={Similarity:F4}, Threshold={Threshold:F4}, Mode={Mode}, Profiles={ProfileCount}",
                    policy.CameraId, result.Similarity, policy.EffectiveThreshold, policy.Mode, profiles.Count);
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
                    {
                        threshold = camera.MatchThresholdOverride.Value;
                        _logger.LogDebug("Custom threshold applied. CameraId={CameraId}, Threshold={Threshold:F4}",
                            resolvedCameraId, threshold);
                    }

                    if (!camera.Capabilities.HasFlag(CameraAICapabilities.Face))
                    {
                        mode = CameraRecognitionMode.Disabled;
                        _logger.LogDebug("Face AI capability disabled. CameraId={CameraId}, Capabilities={Capabilities}",
                            resolvedCameraId, camera.Capabilities);
                    }
                }
                else
                {
                    _logger.LogWarning("Camera not found in database. CameraId={CameraId}", id);
                }
            }

            threshold = mode switch
            {
                CameraRecognitionMode.Strict => Math.Min(1.0, threshold + 0.05),
                CameraRecognitionMode.Relaxed => Math.Max(0.0, threshold - 0.05),
                _ => threshold
            };

            return new CameraRecognitionPolicy(resolvedCameraId, mode, threshold);
        }

        private static string BucketizeConfidence(double similarity)
        {
            return similarity switch
            {
                >= HIGH_CONFIDENCE => "High",
                >= MEDIUM_CONFIDENCE => "Medium",
                > 0.0 => "Low",
                _ => "None"
            };
        }
    }
}