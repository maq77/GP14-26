using SSSP.DAL.ValueObjects;
using SSSP.DAL.Enums;
using SSSP.DAL.Abstractions;


namespace SSSP.DAL.Models;

public class Sensor : IEntity<int>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public SensorType Type { get; set; }

    public Location Location { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReadingAt { get; set; }
}
