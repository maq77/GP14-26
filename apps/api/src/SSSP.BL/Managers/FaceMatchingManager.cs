using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.Faces;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.Records;
using SSSP.DAL.Models;
using System.Threading;


namespace SSSP.BL.Managers
{
    public sealed class FaceMatchingManager : IFaceMatchingManager
    {
        private readonly double _defaultThreshold;
        private readonly ILogger<FaceMatchingManager> _logger;

        public double DefaultThreshold => _defaultThreshold;

        public FaceMatchingManager(
            double defaultThreshold,
            ILogger<FaceMatchingManager> logger)
        {
            if (defaultThreshold <= 0 || defaultThreshold > 1)
                throw new ArgumentOutOfRangeException(
                    nameof(defaultThreshold),
                    defaultThreshold,
                    "Default threshold must be in (0, 1].");

            _defaultThreshold = defaultThreshold;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public FaceMatchResult Match(
            IReadOnlyList<float> probeEmbedding,
            IReadOnlyList<FaceProfileSnapshot> knownProfiles,
            double? thresholdOverride = null)
        {
            var sw = Stopwatch.StartNew();

            if (probeEmbedding == null || probeEmbedding.Count == 0)
            {
                _logger.LogWarning(
                    "Face matching requested with empty probe embedding. Profiles={ProfilesCount}",
                    knownProfiles?.Count ?? 0);

                return new FaceMatchResult(false, null, null, 0.0);
            }

            var profiles = knownProfiles ?? Array.Empty<FaceProfileSnapshot>();

            if (profiles.Count == 0)
            {
                _logger.LogInformation(
                    "Face matching skipped. No FaceProfiles available. EmbeddingDim={EmbeddingDim}",
                    probeEmbedding.Count);

                return new FaceMatchResult(false, null, null, 0.0);
            }

            var threshold = thresholdOverride ?? _defaultThreshold;
            if (threshold <= 0 || threshold > 1)
            {
                _logger.LogWarning(
                    "Invalid thresholdOverride={OverrideThreshold}. Falling back to DefaultThreshold={DefaultThreshold}.",
                    thresholdOverride,
                    _defaultThreshold);

                threshold = _defaultThreshold;
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EmbeddingDim"] = probeEmbedding.Count,
                ["ProfilesCount"] = profiles.Count,
                ["Threshold"] = threshold
            });

            _logger.LogDebug(
                "Starting face matching. EmbeddingDim={EmbeddingDim}, Profiles={ProfilesCount}, Threshold={Threshold:F4}",
                probeEmbedding.Count,
                profiles.Count,
                threshold);

            // Convert probe to array + precompute norm for span-based math
            var probeArray = probeEmbedding.ToArray();
            var probeNorm = ComputeNorm(probeArray);

            if (probeNorm == 0)
            {
                _logger.LogWarning("Face matching aborted. Probe embedding norm is zero.");
                return new FaceMatchResult(false, null, null, 0.0);
            }

            // Diagnostics counters
            var totalEmbeddings = 0;
            var validEmbeddings = 0;
            var emptyEmbeddings = 0;
            var profilesWithEmbeddings = 0;
            var profilesWithoutEmbeddings = 0;

            FaceProfileSnapshot? bestProfile = null;
            double bestSimilarity = double.NegativeInfinity;

            foreach (var profile in profiles)
            {
                if (profile == null)
                    continue;

                if (profile.Embeddings == null || profile.Embeddings.Count == 0)
                {
                    profilesWithoutEmbeddings++;

                    _logger.LogDebug(
                        "Profile has NO embeddings. ProfileId={ProfileId}, UserId={UserId}, UserName={UserName}",
                        profile.Id,
                        profile.UserId,
                        profile.UserName ?? "N/A");

                    continue;
                }

                profilesWithEmbeddings++;

                var profileEmbeddingCount = 0;
                var profileValidEmbeddings = 0;
                var profileBestSimilarity = double.NegativeInfinity;

                foreach (var emb in profile.Embeddings)
                {
                    totalEmbeddings++;
                    profileEmbeddingCount++;

                    if (emb?.Vector == null || emb.Vector.Length == 0)
                    {
                        emptyEmbeddings++;

                        _logger.LogWarning(
                            "Empty embedding vector. ProfileId={ProfileId}, EmbeddingId={EmbeddingId}, UserId={UserId}",
                            profile.Id,
                            emb?.Id,
                            profile.UserId);

                        continue;
                    }

                    var storedSpan = emb.Vector.AsSpan();

                    if (storedSpan.Length != probeArray.Length)
                    {
                        _logger.LogError(
                            "Embedding dimension mismatch. ProfileId={ProfileId}, EmbeddingId={EmbeddingId}, ExpectedDim={Expected}, StoredDim={Stored}, UserId={UserId}",
                            profile.Id,
                            emb.Id,
                            probeArray.Length,
                            storedSpan.Length,
                            profile.UserId);

                        continue;
                    }

                    validEmbeddings++;
                    profileValidEmbeddings++;

                    var similarity = ComputeCosineSimilarity(
                        probeArray,
                        probeNorm,
                        storedSpan);

                    if (similarity > profileBestSimilarity)
                    {
                        profileBestSimilarity = similarity;
                    }

                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestProfile = profile;
                    }

                    _logger.LogTrace(
                        "Embedding compared. ProfileId={ProfileId}, EmbeddingId={EmbeddingId}, UserId={UserId}, Similarity={Similarity:F4}",
                        profile.Id,
                        emb.Id,
                        profile.UserId,
                        similarity);
                }

                _logger.LogDebug(
                        "Profile processed. ProfileId={ProfileId}, UserId={UserId}, UserName={UserName}, TotalEmbeddings={TotalEmbeddings}, ValidEmbeddings={ValidEmbeddings}, BestSimilarity={BestSimilarity:F4}",
                        profile.Id,
                        profile.UserId,
                        profile.UserName,
                        profileEmbeddingCount,
                        profileValidEmbeddings,
                        double.IsNegativeInfinity(profileBestSimilarity) ? 0.0 : profileBestSimilarity);
            }

            sw.Stop();

            if (bestProfile == null || double.IsNegativeInfinity(bestSimilarity))
            {
                _logger.LogInformation(
                    "Face matching completed. No valid embeddings across {ProfilesCount} profiles. ProfilesWithEmbeddings={ProfilesWithEmbeddings}, ProfilesWithoutEmbeddings={ProfilesWithoutEmbeddings}, TotalEmbeddings={TotalEmbeddings}, ValidEmbeddings={ValidEmbeddings}, EmptyEmbeddings={EmptyEmbeddings}, ElapsedMs={ElapsedMs}",
                    profiles.Count,
                    profilesWithEmbeddings,
                    profilesWithoutEmbeddings,
                    totalEmbeddings,
                    validEmbeddings,
                    emptyEmbeddings,
                    sw.ElapsedMilliseconds);

                if (profiles.Count > 0)
                    LogProfileDetails(profiles);

                return new FaceMatchResult(false, null, null, 0.0);
            }

            var isMatch = bestSimilarity >= threshold;

            _logger.LogInformation(
                     "Face matching completed. TotalProfiles={ProfileCount}, ProfilesWithEmbeddings={ProfilesWithEmbeddings}, ValidEmbeddings={ValidEmbeddings}, BestSimilarity={BestSimilarity:F4}, Threshold={Threshold:F4}, IsMatch={IsMatch}, MatchedUserId={UserId}, MatchedUserName={UserName}, MatchedFaceProfileId={FaceProfileId}, ElapsedMs={ElapsedMs}",
                     profiles.Count,
                     profilesWithEmbeddings,
                     validEmbeddings,
                     bestSimilarity,
                     threshold,
                     isMatch,
                     bestProfile.UserId,
                     bestProfile.FullName,
                     bestProfile.Id,
                     sw.ElapsedMilliseconds);


            if (isMatch)
            {
                return new FaceMatchResult(
                    true,
                    bestProfile.UserId,
                    bestProfile.Id,
                    bestSimilarity);
            }

            return new FaceMatchResult(
                false,
                null,
                null,
                bestSimilarity);
        }

        private void LogProfileDetails(IReadOnlyList<FaceProfileSnapshot> profiles)
        {
            _logger.LogWarning("=== PROFILE DETAILS DIAGNOSTIC ===");

            for (var i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                    continue;

                var embeddingDetails = new List<string>();

                if (profile.Embeddings != null && profile.Embeddings.Count > 0)
                {
                    foreach (var emb in profile.Embeddings)
                    {
                        if (emb == null)
                        {
                            embeddingDetails.Add("[NULL-EMBEDDING]");
                            continue;
                        }

                        string status;

                        if (emb.Vector == null || emb.Vector.Length == 0)
                        {
                            status = "EMPTY";
                        }
                        else
                        {
                            status = $"OK({emb.Vector.Length}D)";
                        }

                        embeddingDetails.Add($"[{emb.Id}: {status}]");
                    }
                }

                _logger.LogWarning(
                    "Profile #{Index}: ProfileId={ProfileId}, UserId={UserId}, UserName={UserName}, IsPrimary={IsPrimary}, EmbeddingCount={EmbeddingCount}, Embeddings={EmbeddingDetails}, CreatedAt={CreatedAt:o}",
                    i + 1,
                    profile.Id,
                    profile.UserId,
                    profile.UserName,
                    profile.IsPrimary,
                    profile.Embeddings?.Count ?? 0,
                    embeddingDetails.Count > 0 ? string.Join(", ", embeddingDetails) : "NONE",
                    profile.CreatedAt);
            }

            _logger.LogWarning("=== END PROFILE DETAILS ===");
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
    }
}
