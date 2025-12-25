using Microsoft.EntityFrameworkCore.ChangeTracking;
using SSSP.DAL.Abstractions;
using SSSP.DAL.Enums;
using System;
using System.Collections.Generic;

namespace SSSP.DAL.Models
{
    public class Operator : IEntity<int>
    {
        public int Id { get; set; }
        public string? Name { get; set; } = string.Empty;
        public OperatorType? Type { get; set; }
        public string? Location { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public bool? IsActive { get; set; } = true;

        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
        public ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();
        public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    }
}
