using Microsoft.AspNetCore.Identity;
using SSSP.DAL.Abstractions;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;

public class User : IdentityUser<Guid>, IEntity<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public UserRole? Role { get; set; } = UserRole.User;

    public int? OperatorId { get; set; }
    public Operator? Operator { get; set; }

    public DateTime? CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    public ICollection<FaceProfile> FaceProfiles { get; set; } = new List<FaceProfile>();
}
