using SSSP.BL.DTOs.Tracking;

namespace SSSP.BL.Managers.Interfaces
{
    public interface IFaceTrackingManager
    {
        UserTrackingSession Track(
            Guid userId,
            Guid faceProfileId,
            float similarity,
            string cameraId,
            string zoneId,
            DateTime seenAtUtc);

        UserTrackingSession? TryFindRecentUser(
            IReadOnlyList<float> probeEmbedding,
            string cameraId,
            TimeSpan maxAge,
            double similarityThreshold);

        UserTrackingSession? TryFindAcrossZone(
            IReadOnlyList<float> probeEmbedding,
            string cameraId,
            string zoneId,
            TimeSpan maxTravelTime,
            double similarityThreshold);

        IReadOnlyCollection<UserTrackingSession> GetActiveSessions();

        void CleanupExpired();
    }
}
