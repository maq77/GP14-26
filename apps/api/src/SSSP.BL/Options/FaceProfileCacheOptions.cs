namespace SSSP.BL.Options
{
<<<<<<< HEAD
    public sealed class FaceProfileCacheOptions
    {
        // 1 min cache
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

        // 1 min L1 cache MemoryCache expiration used in FaceProfileCache only , temp
        public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan DistributedTtl { get; set; } = TimeSpan.FromMinutes(3);

        public TimeSpan MaxStaleness { get; set; } = TimeSpan.FromMinutes(3);

        public double JitterPercent { get; set; } = 0.20;

        public TimeSpan LockTtl { get; set; } = TimeSpan.FromSeconds(20);

        // warmup
        public bool PreferRedisOnStartup { get; set; } = true;

        public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan NonLeaderEmptyRedisBackoff { get; set; } = TimeSpan.FromMilliseconds(250);

        public bool AllowEmergencyDbRefreshIfStale { get; set; } = false;

=======
    public class FaceProfileCacheOptions
    {
        public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(1);
>>>>>>> main
    }
}
