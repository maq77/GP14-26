namespace SSSP.DAL.ValueObjects;

public sealed record Location(double? Latitude, double? Longitude, string? Address)
{
    public static Location Empty => new(null, null, null);
}
