using System;

namespace SSSP.BL.Options
{
    public sealed class FaceRecognitionOptions
    {
        // General
        public int MinEmbeddingSize { get; set; } = 128;

        // Confidence bucketing
        public double HighConfidenceThreshold { get; set; } = 0.85;
        public double MediumConfidenceThreshold { get; set; } = 0.65;

        // Default matching threshold (used by FaceMatchingManager)
        public double DefaultSimilarityThreshold { get; set; } = 0.65;

        public TrackerOptions Tracker { get; set; } = new();
        public AutoEnrollmentOptions AutoEnrollment { get; set; } = new();

        public sealed class TrackerOptions
        {
            public TimeSpan SessionExpiration { get; set; } = TimeSpan.FromMinutes(5);
            public TimeSpan CacheMaxAge { get; set; } = TimeSpan.FromSeconds(10);
            public double CacheSimilarityThreshold { get; set; } = 0.70;
        }

        public sealed class AutoEnrollmentOptions
        {
            public double MinSimilarity { get; set; } = 0.92;
            public double MinVariationDistance { get; set; } = 0.08;
            public int MaxEmbeddingsPerProfile { get; set; } = 10;
            public TimeSpan MinIntervalBetweenEnroll { get; set; } = TimeSpan.FromMinutes(10);
        }
    }
}
