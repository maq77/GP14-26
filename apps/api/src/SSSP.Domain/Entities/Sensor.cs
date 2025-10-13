using SSSP.Domain.Enums;
using SSSP.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Domain.Entities;

public class Sensor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public SensorType Type { get; set; }
    public Location Location { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastReadingAt { get; set; }
}
