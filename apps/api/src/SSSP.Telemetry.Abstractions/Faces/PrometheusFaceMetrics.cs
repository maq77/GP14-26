using System;
using Prometheus;

namespace SSSP.Telemetry.Abstractions.Faces
{
    public sealed class PrometheusFaceMetrics : IFaceMetrics
    {
        // Verify
        private static readonly Counter VerifyRequestsTotal =
            Metrics.CreateCounter("sssp_verify_requests_total",
                "Total verify requests",
                new CounterConfiguration { LabelNames = new[] { "endpoint", "result" } });

        private static readonly Histogram VerifyDurationMs =
            Metrics.CreateHistogram("sssp_verify_duration_ms",
                "Verify request duration in ms",
                new HistogramConfiguration { LabelNames = new[] { "endpoint" } });

        private static readonly Histogram FacesPerRequest =
            Metrics.CreateHistogram("sssp_verify_faces_per_request",
                "Number of faces per verify request",
                new HistogramConfiguration { LabelNames = new[] { "endpoint" } });

        private static readonly Histogram MatchDurationMs =
            Metrics.CreateHistogram("sssp_match_duration_ms",
                "Face matching duration in ms");

        // AI
        private static readonly Histogram AiExtractDurationMs =
            Metrics.CreateHistogram("sssp_ai_extract_duration_ms",
                "AI extract embedding duration in ms",
                new HistogramConfiguration { LabelNames = new[] { "success", "face_detected", "faces" } });

        // Cache
        private static readonly Gauge CacheProfiles =
            Metrics.CreateGauge("sssp_facecache_profiles",
                "Number of face profiles in current snapshot");

        private static readonly Gauge CacheEmbeddings =
            Metrics.CreateGauge("sssp_facecache_embeddings",
                "Number of embeddings in current snapshot");

        private static readonly Gauge CacheAgeSeconds =
            Metrics.CreateGauge("sssp_facecache_age_seconds",
                "Seconds since last successful refresh");

        private static readonly Gauge CacheRefreshing =
            Metrics.CreateGauge("sssp_facecache_is_refreshing",
                "1 if cache is currently refreshing, else 0");

        private static readonly Gauge CacheVersion =
            Metrics.CreateGauge("sssp_facecache_version",
                "Distributed/in-memory version of face cache snapshot");

        private static readonly Counter CacheRefreshTotal =
            Metrics.CreateCounter("sssp_facecache_refresh_total",
                "Face cache refresh attempts",
                new CounterConfiguration { LabelNames = new[] { "source", "result" } });

        private static readonly Histogram CacheRefreshDurationMs =
            Metrics.CreateHistogram("sssp_facecache_refresh_duration_ms",
                "Face cache refresh duration in ms",
                new HistogramConfiguration { LabelNames = new[] { "source" } });

        private static readonly Counter CacheInvalidateTotal =
            Metrics.CreateCounter("sssp_facecache_invalidate_total",
                "Face cache invalidations");

        // Auto enroll
        private static readonly Counter AutoEnrollTotal =
            Metrics.CreateCounter("sssp_face_auto_enroll_total",
                "Auto enrollment events",
                new CounterConfiguration { LabelNames = new[] { "result", "reason" } });

        // IFaceMetrics implementation

        public void ObserveVerifyDuration(string endpoint, double ms)
            => VerifyDurationMs.WithLabels(endpoint).Observe(ms);

        public void IncrementVerifyRequests(string endpoint, string result)
            => VerifyRequestsTotal.WithLabels(endpoint, result).Inc();

        public void ObserveFacesPerRequest(string endpoint, int faces)
            => FacesPerRequest.WithLabels(endpoint).Observe(faces);

        public void ObserveMatchDuration(double ms)
            => MatchDurationMs.Observe(ms);

        public void ObserveAiExtractDuration(double ms, string success, string faceDetected, int faces)
            => AiExtractDurationMs.WithLabels(success, faceDetected, faces.ToString()).Observe(ms);

        public void SetCacheProfilesCount(int count) => CacheProfiles.Set(count);
        public void SetCacheEmbeddingsCount(int count) => CacheEmbeddings.Set(count);
        public void SetCacheAgeSeconds(double seconds) => CacheAgeSeconds.Set(seconds);
        public void SetCacheRefreshing(bool refreshing) => CacheRefreshing.Set(refreshing ? 1 : 0);
        public void SetCacheVersion(long version) => CacheVersion.Set(version);

        public void IncrementCacheRefresh(string source, string result)
            => CacheRefreshTotal.WithLabels(source, result).Inc();

        public void ObserveCacheRefreshDuration(string source, double ms)
            => CacheRefreshDurationMs.WithLabels(source).Observe(ms);

        public void IncrementCacheInvalidate() => CacheInvalidateTotal.Inc();

        public void IncrementAutoEnroll(string result, string reason)
            => AutoEnrollTotal.WithLabels(result, reason).Inc();
    }
}
