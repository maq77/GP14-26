namespace SSSP.DAL.ValueObjects;

public sealed record Location(double? Latitude, double? Longitude, string? Address)
{
<<<<<<< HEAD
    public static Location Empty => new(null, null, null);
}
=======
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Address { get; init; }

    public Location() { }

    public Location(double? latitude = 32.21, double? longitude = 32.21, string? address = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        Address = address;
    }
}
>>>>>>> main
