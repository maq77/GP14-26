using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Domain.ValueObjects;

public record Location
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Address { get; init; }

    public Location() { }

    public Location(double latitude, double longitude, string? address = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        Address = address;
    }
}