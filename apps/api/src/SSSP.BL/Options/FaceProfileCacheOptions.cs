namespace SSSP.BL.Options
{
    public class FaceProfileCacheOptions
    {
        public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(1);
    }
}
