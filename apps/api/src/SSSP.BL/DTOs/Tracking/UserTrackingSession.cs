namespace SSSP.BL.DTOs.Tracking
{
    public sealed class UserTrackingSession
    {
        public Guid UserId { get; }
        public Guid FaceProfileId { get; private set; }

        public DateTime LastSeenUtc { get; set; }
        public string? LastCameraId { get; set; }

        public HashSet<string> SeenCameras { get; } = new();
        public Dictionary<string, DateTime> VisitedZones { get; } = new();

        private readonly List<float> _similarities = new();

        public IReadOnlyList<float> SimilarityHistory => _similarities;

        public double AvgSimilarity =>
            _similarities.Count == 0 ? 0 : _similarities.Average();

        public UserTrackingSession(Guid userId, Guid faceProfileId)
        {
            UserId = userId;
            FaceProfileId = faceProfileId;
        }

        public void AddSimilarity(float value)
        {
            if (_similarities.Count > 50)
                _similarities.RemoveAt(0);

            _similarities.Add(value);
        }

        public void UpdateProfile(Guid newProfileId)
        {
            FaceProfileId = newProfileId;
        }
    }
}
