using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SSSP.BL.Managers.Interfaces;
using SSSP.DAL.Models;
using SSSP.BL.Records;

namespace SSSP.BL.Managers
{
    public sealed class FaceMatchingManager : IFaceMatchingManager
    {
        private readonly double _defaultThreshold;
        private readonly ILogger<FaceMatchingManager> _logger;

        public double DefaultThreshold => _defaultThreshold;

        public FaceMatchingManager(
            double threshold,
            ILogger<FaceMatchingManager> logger)
        {
            if (threshold <= 0 || threshold > 1)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be in (0, 1].");

            _defaultThreshold = threshold;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public FaceMatchResult Match(
            IReadOnlyList<float> probeEmbedding,
            IEnumerable<FaceProfile> knownProfiles,
            double? thresholdOverride = null)
        {
            if (probeEmbedding is null || probeEmbedding.Count == 0)
            {
                _logger.LogWarning("Face matching requested with empty embedding.");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var profiles = knownProfiles as IList<FaceProfile> ?? knownProfiles.ToList();

            if (profiles.Count == 0)
            {
                _logger.LogInformation("Face matching skipped. No FaceProfiles available.");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var threshold = thresholdOverride ?? _defaultThreshold;

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EmbeddingDim"] = probeEmbedding.Count,
                ["ProfilesCount"] = profiles.Count,
                ["Threshold"] = threshold
            });

            _logger.LogDebug(
                "Starting face matching. EmbeddingDim={EmbeddingDim}, Profiles={ProfilesCount}, Threshold={Threshold}",
                probeEmbedding.Count,
                profiles.Count,
                threshold);

            var probeArray = probeEmbedding.ToArray();
            var probeNorm = ComputeNorm(probeArray);

            if (probeNorm == 0)
            {
                _logger.LogWarning("Face matching aborted. Probe embedding norm is zero.");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            FaceProfile? bestProfile = null;
            double bestSimilarity = double.NegativeInfinity;

            foreach (var profile in profiles)
            {
                if (profile == null)
                    continue;

                var embeddings = profile.Embeddings;
                if (embeddings == null || embeddings.Count == 0)
                    continue;

                foreach (var emb in embeddings)
                {
                    if (emb?.Vector == null || emb.Vector.Length == 0)
                        continue;

                    if (!TryGetFloatSpan(emb.Vector, out var storedSpan))
                    {
                        _logger.LogError(
                            "Invalid embedding length for FaceEmbedding {EmbeddingId} (FaceProfile={FaceProfileId}). Length={Length}",
                            emb.Id,
                            profile.Id,
                            emb.Vector.Length);
                        continue;
                    }

                    if (storedSpan.Length != probeArray.Length)
                    {
                        _logger.LogDebug(
                            "Embedding dimension mismatch for FaceProfile {FaceProfileId} / Embedding {EmbeddingId}. StoredDim={StoredDim}, ProbeDim={ProbeDim}",
                            profile.Id,
                            emb.Id,
                            storedSpan.Length,
                            probeArray.Length);
                        continue;
                    }

                    var similarity = ComputeCosineSimilarity(
                        probeArray,
                        probeNorm,
                        storedSpan);

                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestProfile = profile;
                    }
                }
            }

            if (double.IsNegativeInfinity(bestSimilarity) || bestProfile == null)
            {
                _logger.LogInformation(
                    "Face matching completed. No valid embeddings across {ProfilesCount} profiles.",
                    profiles.Count);
                return new FaceMatchResult(false, null, null, 0.0);
            }

            if (bestSimilarity >= threshold)
            {
                _logger.LogInformation(
                    "Face match success. UserId={UserId}, FaceProfileId={FaceProfileId}, Similarity={Similarity}, Threshold={Threshold}",
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSimilarity,
                    threshold);

                return new FaceMatchResult(
                    true,
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSimilarity);
            }

            _logger.LogInformation(
                "Face match failed. BestSimilarity={Similarity}, Threshold={Threshold}",
                bestSimilarity,
                threshold);

            return new FaceMatchResult(
                false,
                null,
                null,
                bestSimilarity);
        }

        private static double ComputeNorm(ReadOnlySpan<float> vector)
        {
            double sumSq = 0;
            for (var i = 0; i < vector.Length; i++)
            {
                var v = vector[i];
                sumSq += (double)v * v;
            }
            return Math.Sqrt(sumSq);
        }

        private static double ComputeCosineSimilarity(
            ReadOnlySpan<float> probe,
            double probeNorm,
            ReadOnlySpan<float> stored)
        {
            double dot = 0;
            double storedNormSq = 0;

            for (var i = 0; i < probe.Length; i++)
            {
                var a = probe[i];
                var b = stored[i];

                dot += (double)a * b;
                storedNormSq += (double)b * b;
            }

            var storedNorm = Math.Sqrt(storedNormSq);
            var denom = probeNorm * storedNorm;

            if (denom <= 0)
                return 0.0;

            return dot / denom;
        }

        private static bool TryGetFloatSpan(byte[] bytes, out ReadOnlySpan<float> span)
        {
            if (bytes.Length == 0 || (bytes.Length % sizeof(float)) != 0)
            {
                span = ReadOnlySpan<float>.Empty;
                return false;
            }

            span = MemoryMarshal.Cast<byte, float>(bytes);
            return true;
        }
    }
}
