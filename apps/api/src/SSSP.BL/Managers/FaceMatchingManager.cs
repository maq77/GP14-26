using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SSSP.DAL.Models;

namespace SSSP.BL.Managers
{
    public record FaceMatchResult(
        bool IsMatch,
        Guid? UserId,
        Guid? FaceProfileId,
        double Similarity
    );

    public sealed class FaceMatchingManager
    {
        private readonly double _threshold;
        private readonly ILogger<FaceMatchingManager> _logger;

        public FaceMatchingManager(
            double threshold,
            ILogger<FaceMatchingManager> logger)
        {
            _threshold = threshold;
            _logger = logger;
        }

        public FaceMatchResult Match(
            IReadOnlyList<float> embedding,
            IEnumerable<FaceProfile> knownProfiles)
        {
            if (embedding == null || embedding.Count == 0)
            {
                _logger.LogWarning("Face matching requested with empty embedding");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            var profiles = knownProfiles as IList<FaceProfile> ?? knownProfiles.ToList();

            if (profiles.Count == 0)
            {
                _logger.LogInformation("Face matching skipped. No known profiles in database");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            _logger.LogDebug(
                "Face matching started. EmbeddingDim={Dim} ProfilesCount={Count} Threshold={Threshold}",
                embedding.Count,
                profiles.Count,
                _threshold);

            var embArr = embedding.ToArray();
            var embNorm = Norm(embArr);

            if (embNorm == 0)
            {
                _logger.LogWarning("Face matching aborted. Embedding norm is zero");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            FaceProfile? bestProfile = null;
            double bestSim = -1.0;

            foreach (var profile in profiles)
            {
                float[]? vec;

                try
                {
                    vec = JsonSerializer.Deserialize<float[]>(profile.EmbeddingJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to deserialize embedding for FaceProfile {FaceProfileId}",
                        profile.Id);
                    continue;
                }

                if (vec == null || vec.Length != embArr.Length)
                {
                    _logger.LogDebug(
                        "Embedding dimension mismatch for FaceProfile {FaceProfileId}. StoredDim={StoredDim} ProbeDim={ProbeDim}",
                        profile.Id,
                        vec?.Length ?? 0,
                        embArr.Length);
                    continue;
                }

                var sim = CosineSimilarity(embArr, vec, embNorm);

                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestProfile = profile;
                }
            }

            if (bestProfile != null && bestSim >= _threshold)
            {
                _logger.LogInformation(
                    "Face match success. UserId={UserId} FaceProfileId={FaceProfileId} Similarity={Similarity}",
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSim);

                return new FaceMatchResult(
                    true,
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSim);
            }

            _logger.LogInformation(
                "Face match failed. BestSimilarity={Similarity} Threshold={Threshold}",
                bestSim,
                _threshold);

            return new FaceMatchResult(
                false,
                null,
                null,
                bestSim);
        }

        private static double Norm(float[] v)
        {
            double sumSq = 0;
            for (int i = 0; i < v.Length; i++)
                sumSq += (double)v[i] * v[i];
            return Math.Sqrt(sumSq);
        }

        private static double CosineSimilarity(float[] a, float[] b, double normA)
        {
            double dot = 0;
            double normBSq = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
                normBSq += (double)b[i] * b[i];
            }

            var normB = Math.Sqrt(normBSq);
            var denom = normA * normB;
            if (denom == 0) return 0.0;

            return dot / denom;
        }
    }
}
