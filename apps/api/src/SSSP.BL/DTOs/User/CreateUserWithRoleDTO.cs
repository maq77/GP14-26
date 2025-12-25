using SSSP.DAL.Enums;

namespace SSSP.BL.DTOs.User
{
    public class CreateUserWithRoleDTO
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? OperatorId { get; set; }
        public UserRole Role { get; set; } = UserRole.User;
    }
}
