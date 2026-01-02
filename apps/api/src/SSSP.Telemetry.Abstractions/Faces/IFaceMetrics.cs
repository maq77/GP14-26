using System;

namespace SSSP.Telemetry.Abstractions.Faces
{
    public interface IFaceMetrics
    {
        // Verify
        void ObserveVerifyDuration(string endpoint, double ms);
        void IncrementVerifyRequests(string endpoint, string result);
        void ObserveFacesPerRequest(string endpoint, int faces);
        void ObserveMatchDuration(double ms);

        // AI
        void ObserveAiExtractDuration(double ms, string success, string faceDetected, int faces);

        // Cache
        void SetCacheProfilesCount(int count);
        void SetCacheEmbeddingsCount(int count);
        void SetCacheAgeSeconds(double seconds);
        void SetCacheRefreshing(bool refreshing);
        void SetCacheVersion(long version);

        void IncrementCacheRefresh(string source, string result);
        void ObserveCacheRefreshDuration(string source, double ms);
        void IncrementCacheInvalidate();

        // Auto-enroll
        void IncrementAutoEnroll(string result, string reason);
    }
}
