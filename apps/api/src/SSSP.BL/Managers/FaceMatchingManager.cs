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

    public class FaceMatchingManager
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
            _logger.LogDebug(
                "Face matching started. Embedding dimension {Dim}, Profiles count {Count}",
                embedding.Count,
                knownProfiles.Count());

            FaceProfile? bestProfile = null;
            double bestSim = -1.0;

            var embArr = embedding.ToArray();
            var embNorm = Norm(embArr);

            if (embNorm == 0)
            {
                _logger.LogWarning("Embedding norm is zero. Matching aborted");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            foreach (var profile in knownProfiles)
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
                        "Embedding dimension mismatch for FaceProfile {FaceProfileId}",
                        profile.Id);
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
                    "Face match success. User {UserId}, FaceProfile {FaceProfileId}, Similarity {Similarity}",
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSim);

                return new FaceMatchResult(
                    true,
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSim
                );
            }

            _logger.LogInformation(
                "Face match failed. Best similarity {Similarity}, Threshold {Threshold}",
                bestSim,
                _threshold);

            return new FaceMatchResult(
                false,
                null,
                null,
                bestSim
            );
        }

        private static double Norm(float[] v)
        {
            double sumSq = 0;
            for (int i = 0; i < v.Length; i++)
                sumSq += (double)v[i] * v[i];
            return Math.Sqrt(sumSq);
        }

        private static double Norm(double[] v)
        {
            double sumSq = 0;
            for (int i = 0; i < v.Length; i++)
                sumSq += v[i] * v[i];
            return Math.Sqrt(sumSq);
        }

        private static double CosineSimilarity(float[] a, float[] b, double normA)
        {
            double dot = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
            }

            var normB = Norm(b.Select(x => (double)x).ToArray());
            var denom = normA * normB;
            if (denom == 0) return 0.0;
            return dot / denom;
        }
    }
}
