using SSSP.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSP.Domain.Entities;

public class Operator
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public OperatorType Type { get; set; }
    public string Location { get; set; } = string.Empty;
    public string[] EnabledModules { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}
