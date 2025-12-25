using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Abstractions;
using Sssp.Ai.Face;
using SSSP.BL.DTOs.Tracking;
using SSSP.BL.Interfaces;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.Options;
using SSSP.BL.Records;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.Persistence.Interfaces;
using System.Linq;


namespace SSSP.BL.Services
{
    public sealed class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly IAIFaceClient _ai;
        private readonly IFaceMatchingManager _matcher;
        private readonly IFaceProfileCache _faceProfileCache;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<FaceRecognitionService> _logger;
        private readonly TelemetryClient _telemetry;
        private readonly IFaceTrackingManager _tracking;
        private readonly IFaceAutoEnrollmentService _autoEnrollment;
        private readonly FaceRecognitionOptions _options;


        /*private const int MIN_EMBEDDING_SIZE = 128;
        private const double AUTO_ENROLL_MIN_SIMILARITY = 0.92;
        private const double HIGH_CONFIDENCE = 0.85;
        private const double MEDIUM_CONFIDENCE = 0.65;*/

        public FaceRecognitionService(
            IAIFaceClient ai,
            IFaceMatchingManager matcher,
            IFaceProfileCache faceProfileCache,
            IUnitOfWork uow,
            ILogger<FaceRecognitionService> logger,
            TelemetryClient telemetry,
            IFaceTrackingManager tracking,
            IFaceAutoEnrollmentService autoEnrollment,
            IOptions<FaceRecognitionOptions> options)
        {
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _faceProfileCache = faceProfileCache ?? throw new ArgumentNullException(nameof(faceProfileCache));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
            _autoEnrollment = autoEnrollment ?? throw new ArgumentNullException(nameof(autoEnrollment));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

                var response = await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

                if (!TryGetBestEmbedding(response, cameraId, out var embedding))
                {
                    sw.Stop();

                    _logger.LogWarning(
                            "Face extraction/selection failed. CameraId={CameraId}, FaceDetected={FaceDetected}, Faces={Faces}, ErrorCode={ErrorCode}, ElapsedMs={ElapsedMs}",
                            cameraId ?? "N/A",
                            response?.FaceDetected ?? false,
                            response?.Faces?.Count ?? 0,
                            response?.ErrorCode,
                            sw.ElapsedMilliseconds);

                    return new FaceMatchResult(false, null, null, 0.0);
                }

                // optional metrics log
                var metrics = response.Metrics;
                if (metrics != null)
                {
                    _logger.LogDebug(
                        "AI Metrics. CameraId={CameraId}, DetectionMs={DetectionMs:F2}, EmbeddingMs={EmbeddingMs:F2}, PreMs={PreMs:F2}, TotalMs={TotalMs:F2}, FacesDetected={FacesDetected}",
                        cameraId ?? "N/A",
                        metrics.DetectionMs,
                        metrics.EmbeddingMs,
                        metrics.PreprocessingMs,
                        metrics.TotalMs,
                        metrics.FacesDetected);
                }

                var result = await MatchInternalAsync(embedding, cameraId, ct);

                sw.Stop();

                TrackFaceRecognitionMetrics(
                    cameraId,
                    result.IsMatch,
                    result.Similarity,
                    sw.ElapsedMilliseconds,
                    mode: "SingleImage");

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

        public async Task<IReadOnlyList<FaceRecognitionHit>> VerifyManyAsync(
            byte[] image,
            string cameraId,
            CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
            {
                _logger.LogWarning(
                    "VerifyMany called with empty image. CameraId={CameraId}",
                    cameraId ?? "N/A");
                return Array.Empty<FaceRecognitionHit>();
            }

            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Multi-face verification started. CameraId={CameraId}, ImageSize={ImageSize}, Mode=SingleImage",
                    cameraId,
                    image.Length);

                var response = await _ai.ExtractEmbeddingAsync(image, cameraId, ct);

                var hits = await VerifyManyInternalAsync(response, cameraId, ct);

                sw.Stop();

                // Track metrics based on best similarity (if any)
                if (hits.Count > 0)
                {
                    var best = hits
                        .OrderByDescending(h => h.Match.Similarity)
                        .First();

                    TrackFaceRecognitionMetrics(
                        cameraId,
                        best.Match.IsMatch,
                        best.Match.Similarity,
                        sw.ElapsedMilliseconds,
                        mode: "MultiImage");
                }
                else
                {
                    TrackFaceRecognitionMetrics(
                        cameraId,
                        isMatch: false,
                        similarity: 0.0,
                        elapsedMs: sw.ElapsedMilliseconds,
                        mode: "MultiImage");
                }

                _logger.LogInformation(
                    "Multi-face verification completed. CameraId={CameraId}, Faces={Faces}, ElapsedMs={ElapsedMs}",
                    cameraId,
                    hits.Count,
                    sw.ElapsedMilliseconds);

                return hits;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning(
                    "Multi-face verification cancelled. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "Multi-face verification failed. CameraId={CameraId}, ElapsedMs={ElapsedMs}",
                    cameraId,
                    sw.ElapsedMilliseconds);
                throw;
            }
        }
        public Task<IReadOnlyList<FaceRecognitionHit>> VerifyManyFromEmbeddingsAsync(
                FaceEmbeddingResponse response,
                string cameraId,
                CancellationToken ct = default)
        {
                return VerifyManyInternalAsync(response, cameraId, ct);
        }

        public async Task<FaceMatchResult> VerifyEmbeddingAsync(
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct = default)
        {
            if (embedding == null || embedding.Count < _options.MinEmbeddingSize)
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

                TrackFaceRecognitionMetrics(
                    cameraId,
                    result.IsMatch,
                    result.Similarity,
                    sw.ElapsedMilliseconds,
                    mode: "SingleImage");

                ///////////////////Line 153////////////////Last edit was here ,, to make usser name instead of user id , or add both , make USER Navigation Properity to Face Macth result @ Face Matching Manager
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

        public async Task<IReadOnlyList<FaceMatchResult>> VerifyEmbeddingsBatchAsync(
            IReadOnlyList<IReadOnlyList<float>> embeddings,
            string cameraId,
            CancellationToken ct = default)
        {
            if (embeddings == null || embeddings.Count == 0)
            {
                return Array.Empty<FaceMatchResult>();
            }

            var sw = Stopwatch.StartNew();

            try
            {
                // Load ALL profiles ONCE (cached)
                var profiles = await _faceProfileCache.GetAllAsync(ct);

                if (profiles.Count == 0)
                {
                    _logger.LogWarning(
                        "Batch verification: no profiles. CameraId={CameraId}, EmbeddingCount={Count}",
                        cameraId, embeddings.Count);

                    return embeddings
                        .Select(_ => new FaceMatchResult(false, null, null, 0.0))
                        .ToList();
                }

                var policy = await ResolveCameraPolicyAsync(cameraId, ct);

                if (policy.Mode == CameraRecognitionMode.Disabled)
                {
                    _logger.LogDebug(
                        "Batch verification disabled. CameraId={CameraId}",
                        policy.CameraId);

                    return embeddings
                        .Select(_ => new FaceMatchResult(false, null, null, 0.0))
                        .ToList();
                }

                var results = new List<FaceMatchResult>(embeddings.Count);

                foreach (var embedding in embeddings)
                {
                    if (embedding == null || embedding.Count < _options.MinEmbeddingSize)
                    {
                        results.Add(new FaceMatchResult(false, null, null, 0.0));
                        continue;
                    }

                    // Try tracker cache first
                    var recent = _tracking.TryFindRecentUser(
                        embedding,
                        policy.CameraId,
                        maxAge: _options.Tracker.CacheMaxAge,
                        similarityThreshold: _options.Tracker.CacheSimilarityThreshold);

                    if (recent != null)
                    {
                        results.Add(new FaceMatchResult(
                            true,
                            recent.UserId,
                            recent.FaceProfileId,
                            recent.AvgSimilarity));
                        continue;
                    }

                    // Fallback to full matching
                    var result = _matcher.Match(embedding, profiles, policy.EffectiveThreshold);

                    if (policy.Mode == CameraRecognitionMode.ObserveOnly)
                    {
                        results.Add(new FaceMatchResult(false, null, null, result.Similarity));
                        continue;
                    }

                    if (result.IsMatch)
                    {
                        _tracking.Track(
                            result.UserId!.Value,
                            result.FaceProfileId!.Value,
                            (float)result.Similarity,
                            policy.CameraId,
                            "Default-Zone",
                            DateTime.UtcNow);

                        // Auto-enrollment
                        if (result.Similarity >= _options.AutoEnrollment.MinSimilarity &&
                            result.UserId.HasValue &&
                            result.FaceProfileId.HasValue)
                        {
                            _ = Task.Run(
                                () => _autoEnrollment.TryAutoEnrollAsync(
                                    result.UserId.Value,
                                    result.FaceProfileId.Value,
                                    embedding,
                                    policy.CameraId,
                                    ct),
                                CancellationToken.None);
                        }
                    }

                    results.Add(result);
                }

                sw.Stop();

                var matchCount = results.Count(r => r.IsMatch);

                _logger.LogInformation(
                    "Batch verification completed. CameraId={CameraId}, Embeddings={Count}, Matches={Matches}, ElapsedMs={ElapsedMs}",
                    cameraId, embeddings.Count, matchCount, sw.ElapsedMilliseconds);

                return results;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Batch verification failed. CameraId={CameraId}, EmbeddingCount={Count}, ElapsedMs={ElapsedMs}",
                    cameraId, embeddings.Count, sw.ElapsedMilliseconds);

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
            // FIRST-LEVEL TRACKER CACHE HIT
            var recent = _tracking.TryFindRecentUser(
                embedding,
                policy.CameraId,
                maxAge: _options.Tracker.CacheMaxAge,
                similarityThreshold: _options.Tracker.CacheSimilarityThreshold);

            if (recent != null)
            {
                _logger.LogDebug(
                    "FAST TRACKER MATCH. UserId={UserId}, AvgSimilarity={Avg:F3}",
                    recent.UserId, recent.AvgSimilarity);

                return new FaceMatchResult(
                    true,
                    recent.UserId,
                    recent.FaceProfileId,
                    recent.AvgSimilarity);
            }

            // SECOND-LEVEL: FULL DB + COSINE MATCH
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

                // Track Request Template in DTO

                _tracking.Track(
                     result.UserId!.Value,
                     result.FaceProfileId!.Value,
                     (float)result.Similarity,
                     policy.CameraId,
                     "Default-Zone",
                     DateTime.UtcNow);


                _logger.LogInformation(
                    "FACE MATCHED & Tracking. CameraId={CameraId}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, Threshold={Threshold:F4}, Confidence={Confidence}, Mode={Mode}, Profiles={ProfileCount}",
                    policy.CameraId, result.UserId, result.FaceProfileId, result.Similarity,
                    policy.EffectiveThreshold, confidence, policy.Mode, profiles.Count);

                // SAFE AUTO-ENROLLMENT HOOK
                if (result.Similarity >= _options.AutoEnrollment.MinSimilarity &&
                    result.UserId.HasValue &&
                    result.FaceProfileId.HasValue)
                {
                    // fire-and-forget
                    _ = Task.Run(
                        () => _autoEnrollment.TryAutoEnrollAsync(
                            result.UserId.Value,
                            result.FaceProfileId.Value,
                            embedding,
                            policy.CameraId,
                            ct),
                        CancellationToken.None);
                }
            }
            else
            {
                _logger.LogDebug(
                    "No face match. CameraId={CameraId}, BestSimilarity={Similarity:F4}, Threshold={Threshold:F4}, Mode={Mode}, Profiles={ProfileCount}",
                    policy.CameraId, result.Similarity, policy.EffectiveThreshold, policy.Mode, profiles.Count);
            }

            return result;
        }



        private async Task<IReadOnlyList<FaceRecognitionHit>> VerifyManyInternalAsync(
            FaceEmbeddingResponse response,
            string cameraId,
            CancellationToken ct)
        {
            var hits = new List<FaceRecognitionHit>();

            if (response is null)
            {
                _logger.LogWarning(
                    "Face embedding response is null. CameraId={CameraId}",
                    cameraId ?? "N/A");
                return hits;
            }

            if (!response.Success || response.ErrorCode != ErrorCode.Unspecified)
            {
                _logger.LogWarning(
                    "Face embedding failed (multi). CameraId={CameraId}, Success={Success}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                    cameraId ?? "N/A",
                    response.Success,
                    response.ErrorCode,
                    response.ErrorMessage ?? "N/A");
                return hits;
            }

            if (!response.FaceDetected || response.Faces.Count == 0)
            {
                _logger.LogInformation(
                    "No faces detected in multi-face embedding response. CameraId={CameraId}, FaceDetected={FaceDetected}, Faces={Faces}",
                    cameraId ?? "N/A",
                    response.FaceDetected,
                    response.Faces.Count);
                return hits;
            }

            var policy = await ResolveCameraPolicyAsync(cameraId, ct);

            if (policy.Mode == CameraRecognitionMode.Disabled)
            {
                _logger.LogDebug(
                    "Recognition disabled (multi). CameraId={CameraId}, Mode={Mode}",
                    policy.CameraId,
                    policy.Mode);
                return hits;
            }

            var profiles = await _faceProfileCache.GetAllAsync(ct);

            if (profiles.Count == 0)
            {
                _logger.LogWarning(
                    "No face profiles available for multi-face matching. CameraId={CameraId}",
                    policy.CameraId);
                return hits;
            }

            foreach (var face in response.Faces)
            {
                var emb = face.EmbeddingVector;

                if (emb == null || emb.Count < _options.MinEmbeddingSize)
                {
                    _logger.LogDebug(
                        "Skipping face without valid embedding. CameraId={CameraId}, FaceId={FaceId}, EmbeddingSize={EmbeddingSize}",
                        cameraId ?? "N/A",
                        face.FaceId,
                        emb?.Count ?? 0);
                    continue;
                }

                FaceMatchResult matchResult;

                // FIRST-LEVEL: tracker cache
                var recent = _tracking.TryFindRecentUser(
                    emb,
                    policy.CameraId,
                    maxAge: _options.Tracker.CacheMaxAge,
                    similarityThreshold: _options.Tracker.CacheSimilarityThreshold);

                if (recent != null)
                {
                    _logger.LogDebug(
                        "FAST TRACKER MATCH (multi). CameraId={CameraId}, FaceId={FaceId}, UserId={UserId}, AvgSimilarity={Avg:F3}",
                        cameraId ?? "N/A",
                        face.FaceId,
                        recent.UserId,
                        recent.AvgSimilarity);

                    matchResult = new FaceMatchResult(
                        true,
                        recent.UserId,
                        recent.FaceProfileId,
                        recent.AvgSimilarity);
                }
                else
                {
                    // SECOND-LEVEL: full DB + cosine match
                    var singleResult = _matcher.Match(emb, profiles, policy.EffectiveThreshold);

                    if (policy.Mode == CameraRecognitionMode.ObserveOnly)
                    {
                        _logger.LogInformation(
                            "Observe-only mode (multi). CameraId={CameraId}, FaceId={FaceId}, Similarity={Similarity:F4}, Threshold={Threshold:F4}, Profiles={ProfileCount}",
                            policy.CameraId,
                            face.FaceId,
                            singleResult.Similarity,
                            policy.EffectiveThreshold,
                            profiles.Count);

                        // Force IsMatch=false in observe-only
                        matchResult = new FaceMatchResult(
                            false,
                            null,
                            null,
                            singleResult.Similarity);
                    }
                    else
                    {
                        if (singleResult.IsMatch)
                        {
                            var confidence = BucketizeConfidence(singleResult.Similarity);

                            _tracking.Track(
                                singleResult.UserId!.Value,
                                singleResult.FaceProfileId!.Value,
                                (float)singleResult.Similarity,
                                policy.CameraId,
                                "Default-Zone",
                                DateTime.UtcNow);

                            _logger.LogInformation(
                                "FACE MATCHED & Tracking (multi). CameraId={CameraId}, FaceId={FaceId}, UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity:F4}, Threshold={Threshold:F4}, Confidence={Confidence}, Mode={Mode}, Profiles={ProfileCount}",
                                policy.CameraId,
                                face.FaceId,
                                singleResult.UserId,
                                singleResult.FaceProfileId,
                                singleResult.Similarity,
                                policy.EffectiveThreshold,
                                confidence,
                                policy.Mode,
                                profiles.Count);

                            // SAFE AUTO-ENROLLMENT HOOK
                            if (singleResult.Similarity >= _options.AutoEnrollment.MinSimilarity &&
                                singleResult.UserId.HasValue &&
                                singleResult.FaceProfileId.HasValue)
                            {
                                _ = Task.Run(
                                    () => _autoEnrollment.TryAutoEnrollAsync(
                                        singleResult.UserId.Value,
                                        singleResult.FaceProfileId.Value,
                                        emb,
                                        policy.CameraId,
                                        ct),
                                    CancellationToken.None);
                            }
                        }
                        else
                        {
                            _logger.LogDebug(
                                "No face match (multi). CameraId={CameraId}, FaceId={FaceId}, BestSimilarity={Similarity:F4}, Threshold={Threshold:F4}, Mode={Mode}, Profiles={ProfileCount}",
                                policy.CameraId,
                                face.FaceId,
                                singleResult.Similarity,
                                policy.EffectiveThreshold,
                                policy.Mode,
                                profiles.Count);
                        }

                        matchResult = singleResult;
                    }
                }

                var bbox = face.Bbox;
                var bboxDto = bbox != null
                    ? new FaceBoundingBox(bbox.X, bbox.Y, bbox.W, bbox.H)
                    : new FaceBoundingBox(0, 0, 0, 0);

                var overallQuality = face.Quality?.OverallScore ?? 0f;

                hits.Add(new FaceRecognitionHit(
                    face.FaceId,
                    bboxDto,
                    matchResult,
                    overallQuality));
            }

            return hits;
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

        private string BucketizeConfidence(double similarity)
        {
            if (similarity >= _options.HighConfidenceThreshold)
                return "High";

            if (similarity >= _options.MediumConfidenceThreshold)
                return "Medium";

            if (similarity > 0.0)
                return "Low";

            return "None";
        }


        private void TrackFaceRecognitionMetrics(
            string cameraId,
            bool isMatch,
            double similarity,
            long elapsedMs,
            string mode)
        {
            if (_telemetry == null)
                return;

            var confidence = BucketizeConfidence(similarity);

            var props = new Dictionary<string, string>
            {
                ["CameraId"] = cameraId ?? "N/A",
                ["Result"] = isMatch ? "Match" : "NoMatch",
                ["Confidence"] = confidence,
                ["Mode"] = mode   // "SingleImage" أو "Streaming"
            };

            _telemetry.TrackEvent("FaceRecognitionAttempt", props);

            _telemetry.TrackMetric("FaceRecognitionSimilarity", similarity, props);
            _telemetry.TrackMetric("FaceRecognitionLatencyMs", elapsedMs, props);
        }

        private bool TryGetBestEmbedding(
            FaceEmbeddingResponse response,
            string cameraId,
            out IReadOnlyList<float> embedding)
        {
            embedding = Array.Empty<float>();

            if (response is null)
            {
                _logger.LogWarning(
                    "Face embedding response is null. CameraId={CameraId}",
                    cameraId ?? "N/A");
                return false;
            }

            if (!response.Success || response.ErrorCode != ErrorCode.Unspecified)
            {
                _logger.LogWarning(
                    "Face embedding failed. CameraId={CameraId}, Success={Success}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                    cameraId ?? "N/A",
                    response.Success,
                    response.ErrorCode,
                    response.ErrorMessage ?? "N/A");
                return false;
            }

            if (!response.FaceDetected || response.Faces.Count == 0)
            {
                _logger.LogInformation(
                    "No faces detected in embedding response. CameraId={CameraId}, FaceDetected={FaceDetected}, Faces={Faces}",
                    cameraId ?? "N/A",
                    response.FaceDetected,
                    response.Faces.Count);
                return false;
            }

            // Choose best face:
            // 1) highest quality.overall_score
            // 2) fallback: largest bbox area
            var candidates = response.Faces
                .Where(f => f.EmbeddingVector != null && f.EmbeddingVector.Count >= _options.MinEmbeddingSize)
                .ToList();

            if (candidates.Count == 0)
            {
                _logger.LogWarning(
                    "No valid faces with embeddings found. CameraId={CameraId}, Faces={Faces}",
                    cameraId ?? "N/A",
                    response.Faces.Count);
                return false;
            }

            Face bestFace = candidates
                .OrderByDescending(f => f.Quality?.OverallScore ?? 0f)
                .ThenByDescending(f =>
                {
                    var b = f.Bbox;
                    if (b is null)
                        return 0f;
                    return b.W * b.H;
                })
                .First();

            embedding = bestFace.EmbeddingVector.ToArray();

            _logger.LogDebug(
                "Best face selected. CameraId={CameraId}, FaceId={FaceId}, OverallScore={Score:F3}, Width={Width}, Height={Height}, EmbeddingDim={Dim}",
                cameraId ?? "N/A",
                bestFace.FaceId,
                bestFace.Quality?.OverallScore ?? 0f,
                bestFace.Bbox?.W ?? 0f,
                bestFace.Bbox?.H ?? 0f,
                bestFace.EmbeddingVector.Count);

            return true;
        }

    }
}