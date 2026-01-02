using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using SSSP.BL.Managers.Interfaces;
using SSSP.BL.DTOs.Tracking;
using SSSP.BL.Interfaces;
using SSSP.BL.Options;
using Microsoft.Extensions.Options;

namespace SSSP.BL.Managers
{
    public class FaceTrackingManager : IFaceTrackingManager
    {
        private readonly ConcurrentDictionary<Guid, UserTrackingSession> _sessions = new();
        //private readonly TimeSpan _expiration = TimeSpan.FromMinutes(5);
        private readonly ILogger<FaceTrackingManager> _logger;
        private readonly TelemetryClient _telemetry;
        private readonly ITrackingNotificationService _notifier;
        private readonly FaceRecognitionOptions _options;
        private readonly ICameraTopologyService _topology;


        public FaceTrackingManager(
            ILogger<FaceTrackingManager> logger,
            TelemetryClient telemetry,
            ICameraTopologyService topology,
            ITrackingNotificationService notifier,

            IOptions<FaceRecognitionOptions> options)
        {
            _logger = logger;
            _telemetry = telemetry;
            _notifier = notifier;
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public UserTrackingSession Track(
            Guid userId,
            Guid faceProfileId,
            float similarity,
            string cameraId,
            string zoneId,
            DateTime seenAtUtc)
        {
            var session = _sessions.GetOrAdd(userId, _ => new UserTrackingSession(userId, faceProfileId));

            lock (session)
            {
                session.LastSeenUtc = seenAtUtc;
                session.UpdateProfile(faceProfileId);
                session.AddSimilarity(similarity);

                if (!string.IsNullOrWhiteSpace(cameraId))
                {
                    session.SeenCameras.Add(cameraId);
                    session.LastCameraId = cameraId;
                }

                if (!string.IsNullOrWhiteSpace(zoneId))
                    session.VisitedZones[zoneId] = seenAtUtc;
            }

            _logger.LogInformation(
                "USER TRACKED. UserId={UserId}, FaceProfileId={ProfileId}, Camera={Camera}, Zone={Zone}, AvgSimilarity={Avg:F3}",
                userId, faceProfileId, cameraId, zoneId, session.AvgSimilarity);

            _telemetry.TrackEvent("UserTracked", new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString(),
                ["CameraId"] = cameraId,
                ["ZoneId"] = zoneId
            });

            _telemetry.TrackMetric("FaceTrackingSimilarity", similarity);
            _telemetry?.TrackMetric("ActiveTrackedUsers", _sessions.Count);

            _ = _notifier.NotifyUserTrackedAsync(
                 session,
                 cameraId,
                 CancellationToken.None);

            return session;
        }

        public UserTrackingSession? TryFindRecentUser(
        IReadOnlyList<float> probeEmbedding,
        string cameraId,
        TimeSpan maxAge,
        double similarityThreshold)
        {
            var now = DateTime.UtcNow;

            foreach (var session in _sessions.Values)
            {
                if ((now - session.LastSeenUtc) > maxAge)
                    continue;

                if (!session.SeenCameras.Contains(cameraId))
                    continue; // camera proximity constraint

                // Temporal identity smoothing:
                if (session.AvgSimilarity >= similarityThreshold)
                {
                    _logger.LogDebug(
                        "TRACKER HIT. UserId={UserId}, AvgSimilarity={Avg:F3}",
                        session.UserId, session.AvgSimilarity);

                    _telemetry?.TrackEvent("TrackerCacheHit");
                    return session;
                }
            }

            _telemetry?.TrackEvent("TrackerCacheMiss");
            return null;
        }

        public UserTrackingSession? TryFindAcrossZone(
            IReadOnlyList<float> probeEmbedding,
            string cameraId,
            string zoneId,
            TimeSpan maxTravelTime,
            double similarityThreshold)
        {
            var now = DateTime.UtcNow;

            // 1) SAME CAMERA: reuse existing temporal smoothing
            var sameCameraHit = TryFindRecentUser(
                probeEmbedding,
                cameraId,
                maxTravelTime,
                similarityThreshold);

            if (sameCameraHit != null)
            {
                _logger.LogDebug(
                    "TRACKER SAME-CAMERA HIT via TryFindAcrossZone. UserId={UserId}, CameraId={CameraId}, AvgSimilarity={Avg:F3}",
                    sameCameraHit.UserId,
                    cameraId,
                    sameCameraHit.AvgSimilarity);

                _telemetry?.TrackEvent("CrossCameraReIdSameCameraHit", new Dictionary<string, string>
                {
                    ["UserId"] = sameCameraHit.UserId.ToString(),
                    ["CameraId"] = cameraId,
                    ["ZoneId"] = zoneId
                });

                return sameCameraHit;
            }

            // 2) NEIGHBOR CAMERAS in same topology zone
            var neighborCameras = _topology.GetNeighborCameras(cameraId);
            var topologyZone = _topology.GetZoneId(cameraId);

            if (topologyZone != null &&
                !string.Equals(topologyZone, zoneId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Zone mismatch between tracking and topology. CameraId={CameraId}, TrackerZone={TrackerZone}, TopologyZone={TopologyZone}",
                    cameraId,
                    zoneId,
                    topologyZone);
            }

            if (neighborCameras.Count == 0)
            {
                _logger.LogDebug(
                    "No neighbors found for camera. CameraId={CameraId}, ZoneId={ZoneId}",
                    cameraId,
                    zoneId);

                _telemetry?.TrackEvent("CrossCameraReIdNoNeighbors", new Dictionary<string, string>
                {
                    ["CameraId"] = cameraId,
                    ["ZoneId"] = zoneId
                });

                return null;
            }

            UserTrackingSession? bestSession = null;
            double bestSimilarity = double.NegativeInfinity;

            foreach (var session in _sessions.Values)
            {
                // Skip old sessions
                var age = now - session.LastSeenUtc;
                if (age > maxTravelTime)
                    continue;

                // Must have been seen recently on a neighbor camera
                var fromCamera = session.LastCameraId;
                if (string.IsNullOrWhiteSpace(fromCamera))
                    continue;

                if (!neighborCameras.Contains(fromCamera))
                    continue;

                // Edge weight (seconds) between fromCamera -> current camera
                var edgeSeconds = _topology.GetTravelSeconds(fromCamera, cameraId);

                if (edgeSeconds.HasValue)
                {
                    var edgeMax = TimeSpan.FromSeconds(edgeSeconds.Value);

                    if (age > edgeMax)
                        continue; // too slow for this edge
                }

                var seenOnNeighbor = session.SeenCameras.Overlaps(neighborCameras);
                if (!seenOnNeighbor)
                    continue;

                // Optional: also enforce zone recency
                if (session.VisitedZones.TryGetValue(zoneId, out var lastSeenInZoneUtc))
                {
                    if ((now - lastSeenInZoneUtc) > maxTravelTime)
                        continue;
                }

                if (session.AvgSimilarity < similarityThreshold)
                    continue;

                /*if (session.AvgSimilarity > bestSimilarity || // new version
                    (Math.Abs(session.AvgSimilarity - bestSimilarity) < 1e-6 &&
                    session.LastSeenUtc > (bestSession?.LastSeenUtc ?? DateTime.MinValue)))
                {
                    bestSimilarity = session.AvgSimilarity;
                    bestSession = session;
                }*/

                if (session.AvgSimilarity > bestSimilarity) // old version
                {
                    bestSimilarity = session.AvgSimilarity;
                    bestSession = session;
                }
            }

            if (bestSession == null)
            {
                _logger.LogDebug(
                    "CROSS-CAMERA RE-ID MISS. CameraId={CameraId}, ZoneId={ZoneId}, MaxTravelTimeSeconds={MaxTravelTime}",
                    cameraId,
                    zoneId,
                    maxTravelTime.TotalSeconds);

                _telemetry?.TrackEvent("CrossCameraReIdMiss", new Dictionary<string, string>
                {
                    ["CameraId"] = cameraId,
                    ["ZoneId"] = zoneId
                });

                return null;
            }

            // Compute travel time for metrics
            var travelDuration = now - bestSession.LastSeenUtc;
            var travelSeconds = travelDuration.TotalSeconds;
            var fromCam = bestSession.LastCameraId ?? "unknown";

            _logger.LogInformation(
                "CROSS-CAMERA RE-ID HIT. UserId={UserId}, FromCamera={FromCamera}, ToCamera={ToCamera}, Zone={ZoneId}, TravelSeconds={TravelSeconds:F1}, AvgSimilarity={Avg:F3}",
                bestSession.UserId,
                fromCam,
                cameraId,
                zoneId,
                travelSeconds,
                bestSession.AvgSimilarity);

            // NEW: Publish cross-camera re-ID notification
            _ = PublishCrossCameraReIdAsync(
                bestSession.UserId,
                fromCam,
                cameraId,
                zoneId,
                travelSeconds,
                bestSession.AvgSimilarity);

            _telemetry?.TrackEvent("CrossCameraReIdHit", new Dictionary<string, string>
            {
                ["UserId"] = bestSession.UserId.ToString(),
                ["FromCameraId"] = fromCam,
                ["ToCameraId"] = cameraId,
                ["ZoneId"] = zoneId
            });

            _telemetry?.TrackMetric("CrossCameraReIdHits", 1);
            _telemetry?.TrackMetric("CrossCameraTravelSeconds", travelSeconds);

            return bestSession;
        }


        public void CleanupExpired()
        {
            var threshold = DateTime.UtcNow - _options.Tracker.SessionExpiration;

            foreach (var kv in _sessions)
            {
                if (kv.Value.LastSeenUtc < threshold)
                {
                    _sessions.TryRemove(kv.Key, out _);

                    _logger.LogDebug(
                        "TRACK SESSION EXPIRED. UserId={UserId}",
                        kv.Key);
                }
            }
        }

        public IReadOnlyCollection<UserTrackingSession> GetActiveSessions()
        {
            var now = DateTime.UtcNow;
            return _sessions.Values
                .Where(s => (now - s.LastSeenUtc) < _options.Tracker.SessionExpiration)
                .ToList();
        }


        private async Task PublishCrossCameraReIdAsync(
            Guid userId,
            string fromCamera,
            string toCamera,
            string zoneId,
            double travelSeconds,
            double similarity)
        {
            try
            {
                // TODO: Inject IOutboxWriter via constructor
                // For now, we'll use the notifier service
                _logger.LogInformation(
                    "CROSS-CAMERA RE-ID NOTIFICATION. UserId={UserId}, From={From}, To={To}, TravelSec={Sec}",
                    userId, fromCamera, toCamera, travelSeconds);

                // Telemetry already tracked above, notification will be added when IOutboxWriter is injected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish cross-camera re-ID notification");
            }
        }
    }

}
